import http from 'k6/http';
import {check} from 'k6';
import {Counter, Rate, Trend} from 'k6/metrics';

// Steady-state evaluation load test.
// Goal: measure sustainable RPS (and RPS/core) without polluting results with provisioning traffic.
//
// Key features:
// - Uses static naming (project_a, api_key_a, flag_a_xxx) instead of timestamps
// - Idempotent setup: reuses existing resources across runs for realistic caching
// - Two-phase execution: warmup scenario (populate cache) + measurement scenario (collect metrics)
// - Metrics (p99, etc.) only collected from warmed measurement phase, not cold startup
//
// Default behavior:
// - setup() provisions/reuses minimal dataset (1 project + 1 api key + N flags)
// - warmup scenario runs first to populate cache (30s @ 1000 RPS by default)
// - measurement scenario runs after warmup with actual load (2m @ 12000 RPS by default)
//
// Optional (skip provisioning):
// - API_KEY: an existing X-Api-Key
// - FLAG_KEYS: comma-separated existing flag keys
//
// Common env:
// - BASE_URL=http://api:8080
// - RATE=10000 (measurement phase rate)
// - DURATION=2m (measurement phase duration)
// - WARMUP_DURATION=30s
// - WARMUP_RATE=1000
// - PRE_ALLOCATED_VUS=200
// - MAX_VUS=800
// - PROJECT_NAME_PREFIX=project (results in project_a, project_b, etc.)

const evaluationRequests = new Counter('evaluation_requests');
const evaluationErrors = new Rate('evaluation_errors');
const evaluationDuration = new Trend('evaluation_duration');

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const API_VERSION = __ENV.API_VERSION || '1.0';

const USE_API_VERSION = (__ENV.USE_API_VERSION ?? 'true').toLowerCase() !== 'false';
const API_BASE_PATH_OVERRIDE = (__ENV.API_BASE_PATH || '').trim();

const API_KEY_OVERRIDE = (__ENV.API_KEY || '').trim();
const FLAG_KEYS_RAW = (__ENV.FLAG_KEYS || '').trim();

const RATE = Number(__ENV.RATE || 50000);
const DURATION = __ENV.DURATION || '2m';
const PRE_ALLOCATED_VUS = Number(__ENV.PRE_ALLOCATED_VUS || 75);
const MAX_VUS = Number(__ENV.MAX_VUS || 150);

const USER_COUNT = Number(__ENV.USER_COUNT || 100);
const GROUPS_RAW = (__ENV.GROUPS || 'premium,beta-testers,early-adopters').trim();

// 80/20 distribution
const HOT_USER_PERCENTAGE = Number(__ENV.HOT_USER_PERCENTAGE || 0.2);
const HOT_FLAG_PERCENTAGE = Number(__ENV.HOT_FLAG_PERCENTAGE || 0.2);
const HOT_TRAFFIC_PERCENTAGE = Number(__ENV.HOT_TRAFFIC_PERCENTAGE || 0.8);

// Provisioning controls (only used when API_KEY/FLAG_KEYS not provided)
const PROJECT_NAME_PREFIX = (__ENV.PROJECT_NAME_PREFIX || 'project').trim();
const PROJECT_COUNT = Number(__ENV.PROJECT_COUNT || 1);
const FLAGS_PER_PROJECT = Number(__ENV.FLAGS_PER_PROJECT || 25);
const WARMUP_DURATION = __ENV.WARMUP_DURATION || '30s';
const WARMUP_RATE = Number(__ENV.WARMUP_RATE || 1000);

const GROUPS = GROUPS_RAW.split(',').map((s) => s.trim()).filter(Boolean);
const FLAG_KEYS_OVERRIDE = FLAG_KEYS_RAW ? FLAG_KEYS_RAW.split(',').map((s) => s.trim()).filter(Boolean) : [];

const USER_IDS = Array.from({length: USER_COUNT}, (_, i) => `user-${i + 1}`);
const HOT_USER_COUNT = Math.max(1, Math.ceil(USER_IDS.length * HOT_USER_PERCENTAGE));

export const options = {
    systemTags: ['status', 'method', 'name', 'group', 'scenario', 'expected_response'],
    scenarios: {
        warmup: {
            executor: 'constant-arrival-rate',
            rate: WARMUP_RATE,
            timeUnit: '1s',
            duration: WARMUP_DURATION,
            preAllocatedVUs: Math.ceil(PRE_ALLOCATED_VUS / 4),
            maxVUs: Math.ceil(MAX_VUS / 4),
            exec: 'evaluate',
            startTime: '0s',
            tags: {scenario: 'warmup'},
        },
        measurement: {
            executor: 'constant-arrival-rate',
            rate: RATE,
            timeUnit: '1s',
            duration: DURATION,
            preAllocatedVUs: PRE_ALLOCATED_VUS,
            maxVUs: MAX_VUS,
            exec: 'evaluate',
            startTime: WARMUP_DURATION,
            tags: {scenario: 'measurement'},
        },
    },
    thresholds: {
        'http_req_failed{scenario:measurement}': ['rate<0.01'],
        'http_req_duration{scenario:measurement}': ['p(95)<5', 'p(99)<5'],
        'evaluation_errors{scenario:measurement}': ['rate<0.01'],
        'evaluation_duration{scenario:measurement}': ['p(95)<5', 'p(99)<5'],
    },
};

function withApiVersion(url) {
    if (!USE_API_VERSION) return url;
    return url.includes('?') ? `${url}&api-version=${API_VERSION}` : `${url}?api-version=${API_VERSION}`;
}

function apiUrl(api, pathAndQuery) {
    const url = `${api.baseUrl}${pathAndQuery}`;
    return api.useApiVersion ? withApiVersion(url) : url;
}

function recordEvaluation(res, success) {
    evaluationRequests.add(1);
    evaluationErrors.add(!success);
    const duration = res?.timings?.duration;
    if (Number.isFinite(duration) && duration >= 0) evaluationDuration.add(duration);
}

function createRng(seed) {
    let x = seed | 0;
    return function nextU32() {
        x ^= x << 13;
        x ^= x >>> 17;
        x ^= x << 5;
        return x >>> 0;
    };
}

function resolveApiBase(adminToken) {
    if (API_BASE_PATH_OVERRIDE) {
        return {baseUrl: `${BASE_URL}${API_BASE_PATH_OVERRIDE}`, useApiVersion: USE_API_VERSION};
    }

    const candidates = [
        {baseUrl: `${BASE_URL}/api`, useApiVersion: true}
    ];

    for (const candidate of candidates) {
        const probeUrl = candidate.useApiVersion ? withApiVersion(`${candidate.baseUrl}/projects`) : `${candidate.baseUrl}/projects`;
        const res = http.get(probeUrl, {
            headers: {Authorization: `Bearer ${adminToken}`},
            tags: {name: 'setup/probe-projects'},
        });
        if (res.status !== 404) return candidate;
    }

    return {baseUrl: `${BASE_URL}/api`, useApiVersion: true};
}

function createAdminToken() {
    const res = http.post(
        `${BASE_URL}/dev/token`,
        JSON.stringify({userId: 'k6-admin', email: 'k6admin@example.com', scopes: [], role: 'admin'}),
        {headers: {'Content-Type': 'application/json'}, tags: {name: 'setup/admin-token'}}
    );
    if (res.status !== 200) {
        throw new Error(`Failed to obtain admin token. Status: ${res.status}. Body: ${String(res.body).substring(0, 200)}`);
    }
    return JSON.parse(res.body).token;
}

function getOrCreateProject(api, adminToken, projectName) {
    // Try to get existing project
    const listRes = http.get(apiUrl(api, '/projects'), {
        headers: {Authorization: `Bearer ${adminToken}`},
        tags: {name: 'setup/list-projects'},
    });

    if (listRes.status === 200) {
        const body = JSON.parse(listRes.body);
        let projects = body;

        // Handle different response formats
        if (body && typeof body === 'object' && !Array.isArray(body)) {
            projects = body.projects || body.data || body.items || [];
        }

        if (Array.isArray(projects)) {
            const existing = projects.find(p => p && p.name === projectName);
            if (existing) {
                console.log(`Using existing project: ${projectName} (${existing.id})`);
                return existing;
            }
        }
    }

    // Create new project
    console.log(`Creating new project: ${projectName}`);
    const createRes = http.post(
        apiUrl(api, '/projects'),
        JSON.stringify({
            name: projectName,
            description: 'K6 steady-state evaluation project',
        }),
        {
            headers: {'Content-Type': 'application/json', Authorization: `Bearer ${adminToken}`},
            tags: {name: 'setup/create-project'},
        }
    );
    if (createRes.status !== 200 && createRes.status !== 201) {
        throw new Error(`Failed to create project. Status: ${createRes.status}. Body: ${String(createRes.body).substring(0, 200)}`);
    }
    return JSON.parse(createRes.body);
}

function getOrCreateApiKey(api, adminToken, projectId, projectName) {
    const keyName = `api_key_${projectName}`;

    // Try to get existing API keys
    const listRes = http.get(apiUrl(api, `/projects/${projectId}/apikeys`), {
        headers: {Authorization: `Bearer ${adminToken}`},
        tags: {name: 'setup/list-apikeys'},
    });

    if (listRes.status === 200) {
        const body = JSON.parse(listRes.body);
        let keys = body;

        // Handle different response formats
        if (body && typeof body === 'object' && !Array.isArray(body)) {
            keys = body.apiKeys || body.keys || body.data || body.items || [];
        }

        if (Array.isArray(keys)) {
            const existing = keys.find(k => k && k.name === keyName);
            if (existing && existing.apiKey) {
                console.log(`Using existing API key: ${keyName}`);
                return existing.apiKey;
            } else if (existing) {
                // API key exists but the actual key value is not returned in list
                // We need to create a new one with a different name or delete and recreate
                console.log(`API key ${keyName} exists but key value not available, creating new key with suffix`);
                const newKeyName = `${keyName}_${Date.now()}`;
                const createRes = http.post(
                    apiUrl(api, `/projects/${projectId}/apikeys`),
                    JSON.stringify({name: newKeyName, scopes: 'flags:read flags:write flags:delete'}),
                    {
                        headers: {'Content-Type': 'application/json', Authorization: `Bearer ${adminToken}`},
                        tags: {name: 'setup/create-apikey'},
                    }
                );
                if (createRes.status !== 200 && createRes.status !== 201) {
                    throw new Error(`Failed to create API key. Status: ${createRes.status}. Body: ${String(createRes.body).substring(0, 200)}`);
                }
                return JSON.parse(createRes.body).apiKey;
            }
        }
    }

    // Create new API key
    console.log(`Creating new API key: ${keyName}`);
    const createRes = http.post(
        apiUrl(api, `/projects/${projectId}/apikeys`),
        JSON.stringify({name: keyName, scopes: 'flags:read flags:write flags:delete'}),
        {
            headers: {'Content-Type': 'application/json', Authorization: `Bearer ${adminToken}`},
            tags: {name: 'setup/create-apikey'},
        }
    );
    if (createRes.status !== 200 && createRes.status !== 201) {
        throw new Error(`Failed to create API key. Status: ${createRes.status}. Body: ${String(createRes.body).substring(0, 200)}`);
    }
    return JSON.parse(createRes.body).apiKey;
}

function getOrCreateFlags(api, apiKey, projectId, flagsPerProject) {
    // Use a larger page size to increase chances of finding existing flags
    const listRes = http.get(apiUrl(api, '/feature-flags?first=100'), {
        headers: {'X-Api-Key': apiKey},
        tags: {name: 'setup/list-flags'},
    });

    const existingFlags = [];
    if (listRes.status === 200) {
        const body = JSON.parse(listRes.body);
        const projectIdShort = projectId.substring(0, 8);

        // Handle different response formats (array or object with array property)
        let allFlags = body;
        if (body && typeof body === 'object' && !Array.isArray(body)) {
            // If response is an object, try common property names
            allFlags = body.items || body.flags || body.data || [];
        }

        if (Array.isArray(allFlags)) {
            existingFlags.push(...allFlags.filter(f => f && f.key && f.key.includes(projectIdShort)).map(f => f.key));
        }
    }

    const targetFlagCount = flagsPerProject;
    const flagKeys = [];
    const projectIdShort = projectId.substring(0, 8);

    for (let i = 0; i < targetFlagCount; i++) {
        const flagKey = `flag_${String.fromCharCode(97 + i)}_${projectIdShort}`;
        flagKeys.push(flagKey);

        if (existingFlags.includes(flagKey)) {
            continue; // Skip existing flags
        }

        // Create new flag
        console.log(`Creating flag: ${flagKey}`);
        const createRes = http.post(
            apiUrl(api, '/feature-flags'),
            JSON.stringify({
                key: flagKey,
                description: `Steady flag ${String.fromCharCode(97 + i)}`,
                enabled: true,
                parameters: []
            }),
            {
                headers: {'Content-Type': 'application/json', 'X-Api-Key': apiKey},
                tags: {name: 'setup/create-flag'},
            }
        );

        // If creation fails because it already exists, that's fine for our idempotent setup
        if (createRes.status === 400) {
            const body = String(createRes.body);
            if (body.includes('already exists')) {
                console.log(`Flag ${flagKey} already exists (confirmed by API)`);
                continue;
            }
        }

        if (createRes.status !== 200 && createRes.status !== 201) {
            console.warn(`Unexpected status creating flag ${flagKey}: ${createRes.status}. Body: ${createRes.body}`);
        }
    }

    return flagKeys;
}

export function setup() {
    if (GROUPS.length === 0) throw new Error('GROUPS cannot be empty');

    const health = http.get(`${BASE_URL}/health`, {tags: {name: 'setup/health'}});
    if (health.status !== 200) throw new Error(`Health check failed with status ${health.status}`);

    // If override values are provided, skip provisioning.
    if (API_KEY_OVERRIDE) {
        if (FLAG_KEYS_OVERRIDE.length === 0) throw new Error('Missing FLAG_KEYS (comma-separated) when API_KEY is provided');
        const api = API_BASE_PATH_OVERRIDE ? {
            baseUrl: `${BASE_URL}${API_BASE_PATH_OVERRIDE}`,
            useApiVersion: USE_API_VERSION
        } : {baseUrl: `${BASE_URL}/api`, useApiVersion: true};
        const probe = http.get(apiUrl(api, `/evaluation/${encodeURIComponent(FLAG_KEYS_OVERRIDE[0])}?userId=probe&groups=${encodeURIComponent(GROUPS[0])}`), {
            headers: {'X-Api-Key': API_KEY_OVERRIDE},
            responseType: 'none',
            tags: {name: 'setup/probe-evaluation'},
        });
        if (probe.status !== 200) throw new Error(`Evaluation probe failed with status ${probe.status}`);
        return {
            api,
            apiKey: API_KEY_OVERRIDE,
            flagKeys: FLAG_KEYS_OVERRIDE,
            hotFlagCount: Math.max(1, Math.ceil(FLAG_KEYS_OVERRIDE.length * HOT_FLAG_PERCENTAGE)),
            groups: GROUPS,
        };
    }

    // Provision minimal dataset (same approach as k6.evaluation.js).
    const adminToken = createAdminToken();
    const api = resolveApiBase(adminToken);

    const createdApiKeys = [];
    const createdFlagKeys = [];

    for (let projectIndex = 0; projectIndex < PROJECT_COUNT; projectIndex++) {
        const projectName = PROJECT_COUNT === 1 ? PROJECT_NAME_PREFIX : `${PROJECT_NAME_PREFIX}_${String.fromCharCode(97 + projectIndex)}`;

        const project = getOrCreateProject(api, adminToken, projectName);
        const apiKey = getOrCreateApiKey(api, adminToken, project.id, projectName);
        const flagKeys = getOrCreateFlags(api, apiKey, project.id, FLAGS_PER_PROJECT);

        createdApiKeys.push(apiKey);
        createdFlagKeys.push(...flagKeys);
    }

    if (createdApiKeys.length === 0) throw new Error('No API keys were created during setup.');
    if (createdFlagKeys.length === 0) throw new Error('No flags were created during setup.');

    const apiKey = createdApiKeys[0];
    const probe = http.get(apiUrl(api, `/evaluation/${encodeURIComponent(createdFlagKeys[0])}?userId=probe&groups=${encodeURIComponent(GROUPS[0])}`), {
        headers: {'X-Api-Key': apiKey},
        responseType: 'none',
        tags: {name: 'setup/probe-evaluation'},
    });
    if (probe.status !== 200) throw new Error(`Evaluation probe failed with status ${probe.status}`);

    console.log('Setup complete. Warmup scenario will populate cache before measurement.');

    return {
        api,
        apiKey,
        flagKeys: createdFlagKeys,
        hotFlagCount: Math.max(1, Math.ceil(createdFlagKeys.length * HOT_FLAG_PERCENTAGE)),
        groups: GROUPS,
    };
}

export function evaluate(data) {
    if (!globalThis.__rng) {
        // eslint-disable-next-line no-undef
        globalThis.__rng = createRng((__VU * 2654435761) >>> 0);
    }
    const next = globalThis.__rng;

    const isHotPath = (next() / 0xffffffff) < HOT_TRAFFIC_PERCENTAGE;
    const group = data.groups[next() % data.groups.length];
    const userId = isHotPath ? USER_IDS[next() % HOT_USER_COUNT] : USER_IDS[next() % USER_IDS.length];

    const flagKey = isHotPath ? data.flagKeys[next() % data.hotFlagCount] : data.flagKeys[next() % data.flagKeys.length];

    const res = http.get(apiUrl(data.api, `/evaluation/${encodeURIComponent(flagKey)}?userId=${encodeURIComponent(userId)}&groups=${encodeURIComponent(group)}`), {
        headers: {'X-Api-Key': data.apiKey},
        responseType: 'none',
        tags: {name: 'evaluation'},
    });

    const success = check(res, {'evaluation status is 200': (r) => r.status === 200});
    recordEvaluation(res, success);
}
