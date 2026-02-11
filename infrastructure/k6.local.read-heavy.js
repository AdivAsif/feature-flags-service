import http from 'k6/http';
import {check, sleep} from 'k6';
import {Counter, Rate, Trend} from 'k6/metrics';

// Custom metrics
const featureFlagRequests = new Counter('feature_flag_requests');
const featureFlagErrors = new Rate('feature_flag_errors');
const featureFlagDuration = new Trend('feature_flag_duration');
const evaluationRequests = new Counter('evaluation_requests');
const evaluationDuration = new Trend('evaluation_duration');

// Test configuration - Read-heavy workload for cache testing with multi-tenancy
export const options = {
    stages: [
        {duration: '15s', target: 20},   // Ramp up to 20 users
        {duration: '30s', target: 60},   // Ramp up to 60 users
        {duration: '1m', target: 80},    // Ramp up to 80 users
        {duration: '1m30s', target: 80}, // Stay at 80 users
        {duration: '30s', target: 100},  // Spike to 100 users
        {duration: '30s', target: 100},  // Stay at peak
        {duration: '15s', target: 0},    // Ramp down to 0 users
    ],
    thresholds: {
        http_req_duration: ['p(95)<10', 'p(99)<15'], // 95% under 10ms, 99% under 15ms
        http_req_failed: ['rate<0.05'],              // Error rate under 5%
        feature_flag_errors: ['rate<0.05'],
        feature_flag_duration: ['p(95)<10'],
        evaluation_duration: ['p(95)<8'],             // Evaluations should be very fast (cached)
    },
};

const BASE_URL = __ENV.BASE_URL || 'http://host.docker.internal:5000';

function recordResult(res, success, isEvaluation = false) {
    featureFlagRequests.add(1);
    featureFlagErrors.add(!success);

    if (isEvaluation) {
        evaluationRequests.add(1);
    }

    const duration = res?.timings?.duration;
    if (Number.isFinite(duration) && duration >= 0) {
        featureFlagDuration.add(duration);
        if (isEvaluation) {
            evaluationDuration.add(duration);
        }
    }
}

export function setup() {
    console.log(`Running k6 multi-tenant read-heavy test against: ${BASE_URL}`);
    console.log(`Setting up multi-tenant test environment...`);

    // 1. Health check
    const healthCheck = http.get(`${BASE_URL}/health`, {tags: {name: 'setup/health'}});
    if (healthCheck.status !== 200) {
        throw new Error('API is not healthy');
    }
    console.log('✓ API is healthy');

    // 2. Get admin token for setup
    const adminTokenResponse = http.post(
        `${BASE_URL}/dev/token`,
        JSON.stringify({
            userId: 'k6-admin',
            email: 'k6admin@example.com',
            scopes: [],
            role: 'admin'
        }),
        {
            headers: {'Content-Type': 'application/json'},
            tags: {name: 'setup/admin-token'},
        }
    );

    if (adminTokenResponse.status !== 200) {
        throw new Error('Failed to obtain admin token');
    }

    const adminToken = JSON.parse(adminTokenResponse.body).token;
    console.log('✓ Admin token obtained');

    // 3. Create test projects
    const projects = [];
    const projectNames = ['K6-Load-Test-Project-1', 'K6-Load-Test-Project-2', 'K6-Load-Test-Project-3'];

    for (const projectName of projectNames) {
        const projectResponse = http.post(
            `${BASE_URL}/api/projects`,
            JSON.stringify({
                name: projectName,
                description: `K6 load test project for multi-tenancy testing`
            }),
            {
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${adminToken}`
                },
                tags: {name: 'setup/create-project'}
            }
        );

        if (projectResponse.status === 201 || projectResponse.status === 200) {
            const project = JSON.parse(projectResponse.body);
            projects.push(project);
            console.log(`✓ Created project: ${project.name} (${project.id})`);
        }
    }

    if (projects.length === 0) {
        throw new Error('Failed to create any test projects');
    }

    // 4. Create API keys for each project
    const apiKeys = [];
    for (const project of projects) {
        const apiKeyResponse = http.post(
            `${BASE_URL}/api/projects/${project.id}/apikeys`,
            JSON.stringify({
                name: `K6-Load-Test-Key-${project.name}`,
                scopes: 'flags:read flags:write flags:delete'
            }),
            {
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${adminToken}`
                },
                tags: {name: 'setup/create-apikey'}
            }
        );

        if (apiKeyResponse.status === 201 || apiKeyResponse.status === 200) {
            const apiKeyData = JSON.parse(apiKeyResponse.body);
            apiKeys.push({
                projectId: project.id,
                projectName: project.name,
                apiKey: apiKeyData.apiKey
            });
            console.log(`✓ Created API key for ${project.name}`);
        }
    }

    // 5. Create initial test flags for each project
    const testFlags = ['fast-checkout', 'new-ui', 'premium-features', 'beta-access', 'dark-mode'];

    for (const keyData of apiKeys) {
        for (const flagKey of testFlags) {
            const flagPayload = JSON.stringify({
                key: `${flagKey}-${keyData.projectName}`,
                description: `Test flag for load testing`,
                enabled: Math.random() > 0.5,
                parameters: Math.random() > 0.7 ? [
                    {
                        ruleType: 0, // Percentage
                        ruleValue: String(Math.floor(Math.random() * 100))
                    }
                ] : []
            });

            http.post(
                `${BASE_URL}/feature-flags`,
                flagPayload,
                {
                    headers: {
                        'Content-Type': 'application/json',
                        'X-API-Key': keyData.apiKey
                    },
                    tags: {name: 'setup/create-flags'}
                }
            );
        }
        console.log(`✓ Created ${testFlags.length} test flags for ${keyData.projectName}`);
    }

    console.log('');
    console.log('✓ Multi-tenant setup complete!');
    console.log(`  Projects: ${projects.length}`);
    console.log(`  API Keys: ${apiKeys.length}`);
    console.log(`  Flags per project: ${testFlags.length}`);
    console.log('');

    return {
        adminToken,
        apiKeys,
        testFlags
    };
}

export default function (data) {
    const {adminToken, apiKeys, testFlags} = data;

    // Randomly select a project/API key for multi-tenant testing
    const keyData = apiKeys[Math.floor(Math.random() * apiKeys.length)];
    const apiKey = keyData.apiKey;

    // 90% reads, 10% writes - heavily read-focused for cache stress testing
    const isReadOperation = Math.random() < 0.90;

    if (isReadOperation) {
        const readType = Math.random();

        if (readType < 0.60) {
            // 60% - Evaluate flags with user context (most common operation)
            const flagKey = `${testFlags[Math.floor(Math.random() * testFlags.length)]}-${keyData.projectName}`;
            const userId = `user-${Math.floor(Math.random() * 1000)}`;
            const userGroups = ['premium', 'beta-testers', 'early-adopters'][Math.floor(Math.random() * 3)];

            const res = http.get(
                `${BASE_URL}/evaluation/${flagKey}?userId=${userId}&groups=${userGroups}`,
                {
                    headers: {'X-API-Key': apiKey},
                    tags: {name: 'evaluation', project: keyData.projectName}
                }
            );

            const success = check(res, {
                'evaluation status ok': (r) => r.status === 200 || r.status === 404,
                'evaluation has result': (r) => {
                    try {
                        return r.status === 200 && JSON.parse(r.body).enabled !== undefined;
                    } catch {
                        return false;
                    }
                }
            });

            recordResult(res, success, true);
        } else if (readType < 0.85) {
            // 25% - List all flags (pagination)
            const res = http.get(
                `${BASE_URL}/feature-flags?first=20`,
                {
                    headers: {'X-API-Key': apiKey},
                    tags: {name: 'list-flags', project: keyData.projectName}
                }
            );

            const success = check(res, {
                'list status is 200': (r) => r.status === 200,
                'list has items': (r) => {
                    try {
                        return r.status === 200 && JSON.parse(r.body).items !== undefined;
                    } catch {
                        return false;
                    }
                },
            });

            recordResult(res, success);
        } else {
            // 15% - Get specific flag by key
            const flagKey = `${testFlags[Math.floor(Math.random() * testFlags.length)]}-${keyData.projectName}`;

            const res = http.get(
                `${BASE_URL}/feature-flags/${flagKey}`,
                {
                    headers: {'X-API-Key': apiKey},
                    tags: {name: 'get-flag', project: keyData.projectName}
                }
            );

            const success = check(res, {
                'get flag status ok': (r) => r.status === 200 || r.status === 404,
            });

            recordResult(res, success);
        }
    } else {
        // 10% - Write operations (create/update flags)
        const writeType = Math.random();

        if (writeType < 0.7) {
            // 70% of writes are creates
            const payload = JSON.stringify({
                key: `load-test-flag-${keyData.projectName}-${__VU}-${Date.now()}`,
                description: `Load test flag for VU ${__VU}`,
                enabled: Math.random() > 0.5,
                parameters: []
            });

            const res = http.post(
                `${BASE_URL}/feature-flags`,
                payload,
                {
                    headers: {
                        'Content-Type': 'application/json',
                        'X-API-Key': apiKey
                    },
                    tags: {name: 'create-flag', project: keyData.projectName}
                }
            );

            const success = check(res, {
                'create status is 201': (r) => r.status === 200 || r.status === 201,
            });

            recordResult(res, success);
        } else {
            // 30% of writes are updates
            const flagKey = `${testFlags[Math.floor(Math.random() * testFlags.length)]}-${keyData.projectName}`;
            const payload = JSON.stringify({
                key: flagKey,
                description: `Updated at ${new Date().toISOString()}`,
                enabled: Math.random() > 0.5,
                parameters: []
            });

            const res = http.put(
                `${BASE_URL}/feature-flags/${flagKey}`,
                payload,
                {
                    headers: {
                        'Content-Type': 'application/json',
                        'X-API-Key': apiKey
                    },
                    tags: {name: 'update-flag', project: keyData.projectName}
                }
            );

            const success = check(res, {
                'update status ok': (r) => r.status === 200 || r.status === 404,
            });

            recordResult(res, success);
        }
    }

    sleep(0.1); // Small delay to simulate realistic load
}

export function teardown(data) {
    console.log('');
    console.log('=================================================');
    console.log('  Multi-Tenant Read-Heavy Test Completed!       ');
    console.log('=================================================');
    console.log('');
    console.log('Test Summary:');
    console.log(`  • Projects tested: ${data.apiKeys.length}`);
    console.log(`  • Flags per project: ${data.testFlags.length}`);
    console.log(`  • Read/Write ratio: 90/10`);
    console.log(`  • Primary operation: Flag evaluation (60%)`);
    console.log('');
    console.log('View detailed results:');
    console.log('  • Prometheus: http://localhost:9090');
    console.log('  • Grafana: http://localhost:3000');
    console.log('');
    console.log('Note: Test data remains in database for inspection');
    console.log('');
}
