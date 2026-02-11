import http from 'k6/http';
import {check} from 'k6';
import {Counter, Rate, Trend} from 'k6/metrics';

const evaluationRequests = new Counter('evaluation_requests');
const evaluationErrors = new Rate('evaluation_errors');
const evaluationDuration = new Trend('evaluation_duration');

export const options = {
    systemTags: ['status', 'method', 'name', 'group', 'scenario', 'expected_response'],

    scenarios: {
        evaluation: {
            executor: 'ramping-arrival-rate',
            startRate: 0,
            timeUnit: '1s',
            preAllocatedVUs: 750,
            maxVUs: 1500,
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

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const API_VERSION = __ENV.API_VERSION || '1.0';
const RUN_ID = __ENV.RUN_ID || `${Date.now()}`;

const PROJECT_COUNT = Number(__ENV.PROJECT_COUNT || 3);
const FLAGS_PER_PROJECT = Number(__ENV.FLAGS_PER_PROJECT || 25);

// Realistic access pattern configuration
// 80/20 rule: 80% of traffic hits 20% of data
const HOT_USER_PERCENTAGE = 0.2;  // Top 20% of users
const HOT_FLAG_PERCENTAGE = 0.2;  // Top 20% of flags
const HOT_TRAFFIC_PERCENTAGE = 0.8;  // 80% of requests

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
            console.log(`✓ Using API base: ${candidate.baseUrl} (api-version query: ${candidate.useApiVersion})`);
            return candidate;
        }
    }

    console.warn('⚠ Could not detect API base URL (all candidates returned 404), defaulting to /api with api-version.');
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
        throw new Error(`Failed to obtain admin token. Status: ${res.status}. Body: ${String(res.body).substring(0, 300)}`);
    }

    return JSON.parse(res.body).token;
}

export function setup() {
    console.log(`Running k6 portfolio evaluation test against: ${BASE_URL}`);
    const configuredRate = __ENV.RATE || 10000;
    const configuredDuration = __ENV.DURATION || '1m';
    console.log(`Target rate: ${configuredRate}/s for ${configuredDuration}`);
    console.log(`Projects: ${PROJECT_COUNT}, flags per project: ${FLAGS_PER_PROJECT}`);

    const health = http.get(`${BASE_URL}/health`, {tags: {name: 'setup/health'}});
    if (health.status !== 200) {
        throw new Error(`API is not healthy. Status: ${health.status}. Body: ${String(health.body).substring(0, 200)}`);
    }

    const adminToken = createAdminToken();
    const api = resolveApiBase(adminToken);

    // Create projects
    const projects = [];
    for (let i = 0; i < PROJECT_COUNT; i++) {
        const name = `K6-Portfolio-Project-${i + 1}-${RUN_ID}`;

        const res = http.post(
            apiUrl(api, '/projects'),
            JSON.stringify({
                name,
                description: 'K6 portfolio evaluation project',
            }),
            {
                headers: {
                    'Content-Type': 'application/json',
                    Authorization: `Bearer ${adminToken}`,
                },
                tags: {name: 'setup/create-project'},
            }
        );

        if (res.status !== 200 && res.status !== 201) {
            console.error(`✗ Failed to create project '${name}'. Status: ${res.status}. Body: ${String(res.body).substring(0, 300)}`);
            continue;
        }

        const project = JSON.parse(res.body);
        projects.push(project);
    }

    if (projects.length === 0) {
        throw new Error('Failed to create any test projects.');
    }

    // Create API keys (one per project)
    const apiKeys = [];
    for (const project of projects) {
        const res = http.post(
            apiUrl(api, `/projects/${project.id}/apikeys`),
            JSON.stringify({
                name: `K6-Portfolio-Key-${project.name}`,
                scopes: 'flags:read flags:write flags:delete',
            }),
            {
                headers: {
                    'Content-Type': 'application/json',
                    Authorization: `Bearer ${adminToken}`,
                },
                tags: {name: 'setup/create-apikey'},
            }
        );

        if (res.status !== 200 && res.status !== 201) {
            console.error(`✗ Failed to create API key for project '${project.name}'. Status: ${res.status}. Body: ${String(res.body).substring(0, 300)}`);
            continue;
        }

        const body = JSON.parse(res.body);
        apiKeys.push({
            projectId: project.id,
            projectName: project.name,
            apiKey: body.apiKey,
        });
    }

    if (apiKeys.length === 0) {
        throw new Error('Failed to create any API keys.');
    }

    // Create flags for each project
    const flagsByProject = {};
    for (const keyData of apiKeys) {
        flagsByProject[keyData.projectId] = [];

        for (let i = 0; i < FLAGS_PER_PROJECT; i++) {
            const flagKey = `portfolio-flag-${i + 1}-${keyData.projectName}`;
            flagsByProject[keyData.projectId].push(flagKey);

            const payload = JSON.stringify({
                key: flagKey,
                description: `Portfolio flag ${i + 1}`,
                enabled: true,
                parameters: [],
            });

            http.post(apiUrl(api, '/feature-flags'), payload, {
                headers: {
                    'Content-Type': 'application/json',
                    'X-Api-Key': keyData.apiKey,
                },
                tags: {name: 'setup/create-flag'},
            });
        }
    }

    // Create user pools with realistic distribution
    const userIds = Array.from({length: 100}, (_, i) => `user-${i + 1}`);
    const groups = ['premium', 'beta-testers', 'early-adopters'];

    // Calculate hot/cold data boundaries
    const hotUserCount = Math.ceil(userIds.length * HOT_USER_PERCENTAGE);
    const totalFlags = Object.values(flagsByProject).flat();
    const hotFlagCount = Math.ceil(totalFlags.length * HOT_FLAG_PERCENTAGE);

    console.log('✓ Setup complete.');
    console.log(`  Hot users: ${hotUserCount} of ${userIds.length} (${HOT_USER_PERCENTAGE * 100}%)`);
    console.log(`  Hot flags: ${hotFlagCount} of ${totalFlags.length} (${HOT_FLAG_PERCENTAGE * 100}%)`);
    console.log(`  Expected cache hit rate: ~${HOT_TRAFFIC_PERCENTAGE * 100}%`);

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
    // Determine if this request should hit "hot" data (80%) or "cold" data (20%)
    const isHotPath = Math.random() < HOT_TRAFFIC_PERCENTAGE;

    // Select API key and project
    const keyData = data.apiKeys[Math.floor(Math.random() * data.apiKeys.length)];
    const flags = data.flagsByProject[keyData.projectId];

    // Select flag based on hot/cold distribution
    let featureFlagKey;
    if (isHotPath) {
        // Hot path: Use top 20% of flags (first N flags)
        const hotFlagIndex = Math.floor(Math.random() * Math.ceil(flags.length * HOT_FLAG_PERCENTAGE));
        featureFlagKey = flags[hotFlagIndex];
    } else {
        // Cold path: Use any flag (including hot ones for realism)
        featureFlagKey = flags[Math.floor(Math.random() * flags.length)];
    }

    // Select user based on hot/cold distribution
    let userId;
    if (isHotPath) {
        // Hot path: Use top 20% of users (first N users)
        const hotUserIndex = Math.floor(Math.random() * data.hotUserCount);
        userId = data.userIds[hotUserIndex];
    } else {
        // Cold path: Use any user
        userId = data.userIds[Math.floor(Math.random() * data.userIds.length)];
    }

    // Groups can be random - they're small enough
    const group = data.groups[Math.floor(Math.random() * data.groups.length)];

    const res = http.get(
        apiUrl(data.api, `/evaluation/${featureFlagKey}?userId=${userId}&groups=${group}`),
        {
            headers: {'X-Api-Key': keyData.apiKey},
            tags: {name: 'evaluation'},
        }
    );

    const success = check(res, {
        'evaluation status is 200': (r) => r.status === 200,
        'evaluation has allowed': (r) => {
            try {
                return r.status === 200 && JSON.parse(r.body).allowed !== undefined;
            } catch {
                return false;
            }
        },
    });

    recordEvaluation(res, success);
}