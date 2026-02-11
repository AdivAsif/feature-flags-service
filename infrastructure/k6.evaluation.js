import http from 'k6/http';
import {check} from 'k6';
import {Counter, Rate, Trend} from 'k6/metrics';

// Lightweight evaluation load test
// - Keeps the same custom metrics + thresholds as k6.portfolio-evaluation.js
// - Avoids per-request JSON.parse (major k6 CPU sink at high RPS)
// - Defaults to discarding response bodies (another major k6 CPU/mem sink)

const evaluationRequests = new Counter('evaluation_requests');
const evaluationErrors = new Rate('evaluation_errors');
const evaluationDuration = new Trend('evaluation_duration');

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const API_VERSION = __ENV.API_VERSION || '1.0';

const PROJECT_COUNT = Number(__ENV.PROJECT_COUNT || 3);
const FLAGS_PER_PROJECT = Number(__ENV.FLAGS_PER_PROJECT || 25);
const USER_COUNT = Number(__ENV.USER_COUNT || 100);

// 80/20 distribution (same intent as portfolio script)
const HOT_USER_PERCENTAGE = Number(__ENV.HOT_USER_PERCENTAGE || 0.2);
const HOT_FLAG_PERCENTAGE = Number(__ENV.HOT_FLAG_PERCENTAGE || 0.2);
const HOT_TRAFFIC_PERCENTAGE = Number(__ENV.HOT_TRAFFIC_PERCENTAGE || 0.8);

const PRE_ALLOCATED_VUS = Number(__ENV.PRE_ALLOCATED_VUS || 750);
const MAX_VUS = Number(__ENV.MAX_VUS || 1500);

// Keep evaluation response bodies off by default (much cheaper for k6 at high RPS).
// NOTE: we intentionally do NOT use the global discardResponseBodies option, because setup()
// needs response bodies to parse JSON (token, ids, etc).
const DISCARD_EVAL_RESPONSE_BODIES = (__ENV.DISCARD_RESPONSE_BODIES ?? 'true').toLowerCase() !== 'false';

export const options = {
    systemTags: ['status', 'method', 'name', 'group', 'scenario', 'expected_response'],
    scenarios: {
        evaluation: {
            executor: 'ramping-arrival-rate',
            startRate: 0,
            timeUnit: '1s',
            preAllocatedVUs: PRE_ALLOCATED_VUS,
            maxVUs: MAX_VUS,
            stages: [
                {duration: '30s', target: 5000},
                {duration: '30s', target: 10000},
                {duration: '1m', target: 20000},
                {duration: '1m', target: 20000},
                {duration: '30s', target: 0},
            ],
            exec: 'evaluate',
        },
    },
    thresholds: {
        http_req_failed: ['rate<0.01'],
        http_req_duration: ['p(95)<15', 'p(99)<25'],
        evaluation_errors: ['rate<0.01'],
        evaluation_duration: ['p(95)<15', 'p(99)<25'],
    },
};

function withApiVersion(url) {
    return url.includes('?') ? `${url}&api-version=${API_VERSION}` : `${url}?api-version=${API_VERSION}`;
}

function resolveApiBase(adminToken) {
    const candidates = [
        {baseUrl: `${BASE_URL}/api`, useApiVersion: true},
        {baseUrl: `${BASE_URL}/api/v1`, useApiVersion: false},
        {baseUrl: `${BASE_URL}`, useApiVersion: false},
    ];

    for (const candidate of candidates) {
        const probeUrl = candidate.useApiVersion
            ? withApiVersion(`${candidate.baseUrl}/projects`)
            : `${candidate.baseUrl}/projects`;

        const res = http.get(probeUrl, {
            headers: {Authorization: `Bearer ${adminToken}`},
            tags: {name: 'setup/probe-projects'},
        });

        if (res.status !== 404) {
            return candidate;
        }
    }

    return {baseUrl: `${BASE_URL}/api`, useApiVersion: true};
}

function apiUrl(api, pathAndQuery) {
    const url = `${api.baseUrl}${pathAndQuery}`;
    return api.useApiVersion ? withApiVersion(url) : url;
}

function recordEvaluation(res, success) {
    evaluationRequests.add(1);
    evaluationErrors.add(!success);

    const duration = res?.timings?.duration;
    if (Number.isFinite(duration) && duration >= 0) {
        evaluationDuration.add(duration);
    }
}

function createAdminToken() {
    const res = http.post(
        `${BASE_URL}/dev/token`,
        JSON.stringify({
            userId: 'k6-admin',
            email: 'k6admin@example.com',
            scopes: [],
            role: 'admin',
        }),
        {
            headers: {'Content-Type': 'application/json'},
            tags: {name: 'setup/admin-token'},
        }
    );

    if (res.status !== 200) {
        throw new Error(`Failed to obtain admin token. Status: ${res.status}.`);
    }

    return JSON.parse(res.body).token;
}

// Cheap per-VU RNG (faster than Math.random at high rates)
function createRng(seed) {
    let x = seed | 0;
    return function nextU32() {
        x ^= x << 13;
        x ^= x >>> 17;
        x ^= x << 5;
        return x >>> 0;
    };
}

export function setup() {
    const adminToken = createAdminToken();
    const api = resolveApiBase(adminToken);

    // Create projects
    const projects = [];
    for (let i = 0; i < PROJECT_COUNT; i++) {
        const res = http.post(
            apiUrl(api, '/projects'),
            JSON.stringify({
                name: `k6-eval-project-${i + 1}`,
                description: 'K6 evaluation project',
            }),
            {
                headers: {'Content-Type': 'application/json', Authorization: `Bearer ${adminToken}`},
                tags: {name: 'setup/create-project'},
            }
        );

        if (res.status === 200 || res.status === 201) {
            projects.push(JSON.parse(res.body));
        }
    }

    if (projects.length === 0) {
        throw new Error('Failed to create any test projects.');
    }

    // Create API keys (one per project)
    const apiKeys = [];
    for (const project of projects) {
        const res = http.post(
            apiUrl(api, `/projects/${project.id}/apikeys`),
            JSON.stringify({name: `K6-Eval-Key-${project.name}`, scopes: 'flags:read flags:write flags:delete'}),
            {
                headers: {'Content-Type': 'application/json', Authorization: `Bearer ${adminToken}`},
                tags: {name: 'setup/create-apikey'},
            }
        );

        if (res.status === 200 || res.status === 201) {
            const body = JSON.parse(res.body);
            apiKeys.push({projectId: project.id, apiKey: body.apiKey});
        }
    }

    if (apiKeys.length === 0) {
        throw new Error('Failed to create any API keys.');
    }

    // Create flags per project (minimal parsing, no checks)
    const flagsByProject = {};
    for (const keyData of apiKeys) {
        flagsByProject[keyData.projectId] = [];

        for (let i = 0; i < FLAGS_PER_PROJECT; i++) {
            const flagKey = `eval-flag-${i + 1}-p${keyData.projectId}`;
            flagsByProject[keyData.projectId].push(flagKey);

            http.post(apiUrl(api, '/feature-flags'), JSON.stringify({
                key: flagKey,
                description: `Eval flag ${i + 1}`,
                enabled: true,
                parameters: [],
            }), {
                headers: {'Content-Type': 'application/json', 'X-Api-Key': keyData.apiKey},
                tags: {name: 'setup/create-flag'},
            });
        }
    }

    const userIds = Array.from({length: USER_COUNT}, (_, i) => `user-${i + 1}`);
    const groups = ['premium', 'beta-testers', 'early-adopters'];

    const hotUserCount = Math.max(1, Math.ceil(userIds.length * HOT_USER_PERCENTAGE));
    const totalFlags = Object.values(flagsByProject).flat();
    const hotFlagCount = Math.max(1, Math.ceil(totalFlags.length * HOT_FLAG_PERCENTAGE));

    return {
        api,
        apiKeys,
        flagsByProject,
        userIds,
        groups,
        hotUserCount,
        hotFlagCount,
    };
}

export function evaluate(data) {
    // Per-VU RNG: module globals are per VU in k6.
    if (!globalThis.__rng) {
        // eslint-disable-next-line no-undef
        globalThis.__rng = createRng((__VU * 2654435761) >>> 0);
    }
    const next = globalThis.__rng;

    const isHotPath = (next() / 0xffffffff) < HOT_TRAFFIC_PERCENTAGE;

    const keyData = data.apiKeys[next() % data.apiKeys.length];
    const flags = data.flagsByProject[keyData.projectId];

    const hotFlagsLen = Math.max(1, Math.ceil(flags.length * HOT_FLAG_PERCENTAGE));
    const featureFlagKey = isHotPath ? flags[next() % hotFlagsLen] : flags[next() % flags.length];

    const userId = isHotPath ? data.userIds[next() % data.hotUserCount] : data.userIds[next() % data.userIds.length];
    const group = data.groups[next() % data.groups.length];

    const res = http.get(apiUrl(data.api, `/evaluation/${featureFlagKey}?userId=${userId}&groups=${group}`), {
        headers: {'X-Api-Key': keyData.apiKey},
        responseType: DISCARD_EVAL_RESPONSE_BODIES ? 'none' : 'text',
        tags: {name: 'evaluation'},
    });

    // Keep checks cheap: status only (avoid JSON.parse on every request).
    const success = check(res, {
        'evaluation status is 200': (r) => r.status === 200,
    });

    recordEvaluation(res, success);
}
