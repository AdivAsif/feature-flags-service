import http from 'k6/http';
import {check} from 'k6';
import {Counter, Rate, Trend} from 'k6/metrics';

// Custom metrics
const featureFlagRequests = new Counter('feature_flag_requests');
const featureFlagErrors = new Rate('feature_flag_errors');
const featureFlagDuration = new Trend('feature_flag_duration');

// Test configuration - Lower load for local testing with 16GB RAM and SSD
export const options = {
    stages: [
        {duration: '20s', target: 10},   // Ramp up to 10 users
        {duration: '40s', target: 30},   // Ramp up to 30 users
        {duration: '1m', target: 50},    // Ramp up to 50 users
        {duration: '1m30s', target: 50}, // Stay at 50 users
        {duration: '40s', target: 75},   // Spike to 75 users
        {duration: '40s', target: 75},   // Stay at peak
        {duration: '20s', target: 0},    // Ramp down to 0 users
    ],
    thresholds: {
        http_req_duration: ['p(95)<10', 'p(99)<15'], // 95% under 10ms, 99% under 15ms
        http_req_failed: ['rate<0.05'],              // Error rate under 5%
        feature_flag_errors: ['rate<0.05'],
        feature_flag_duration: ['p(95)<10'],
    },
};

const BASE_URL = __ENV.BASE_URL || 'http://host.docker.internal:5000';
const TOKEN = __ENV.AUTH_TOKEN || '';

function getHeaders() {
    const headers = {
        'Content-Type': 'application/json',
    };

    if (TOKEN) {
        headers['Authorization'] = `Bearer ${TOKEN}`;
    }

    return headers;
}

export function setup() {
    console.log(`Running k6 local mixed test against: ${BASE_URL}`);
    console.log(`Testing health endpoint first...`);

    const healthCheck = http.get(`${BASE_URL}/health`);
    console.log(`Health check status: ${healthCheck.status}`);

    if (healthCheck.status !== 200) {
        console.error(`API health check failed. Status: ${healthCheck.status}`);
        throw new Error('API is not healthy');
    }

    if (!TOKEN) {
        console.log('Attempting to get dev token...');
        const devTokenResponse = http.post(
            `${BASE_URL}/dev/token`,
            JSON.stringify({
                userId: 'local-test-user',
                email: 'localtest@example.com',
                scopes: ['flags:read', 'flags:write', 'flags:delete'],
                role: 'admin'
            }),
            {
                headers: {'Content-Type': 'application/json'},
            }
        );

        if (devTokenResponse.status === 200) {
            const body = JSON.parse(devTokenResponse.body);
            console.log('✓ Auth token obtained successfully');
            return {token: body.token};
        } else {
            throw new Error('Failed to obtain auth token');
        }
    }

    return {token: TOKEN};
}

export default function (data) {
    const token = data.token;
    const headers = {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${token}`
    };

    // 70% reads, 30% writes - mixed workload
    const isReadOperation = Math.random() < 0.7;

    if (isReadOperation) {
        // Read operations: Get all flags or evaluate
        const readType = Math.random();
        
        if (readType < 0.6) {
            // Get all feature flags with pagination
            const res = http.get(`${BASE_URL}/feature-flags?first=20`, {headers});
            
            featureFlagRequests.add(1);
            featureFlagDuration.add(res.timings.duration);

            const success = check(res, {
                'status is 200': (r) => r.status === 200,
                'has items': (r) => {
                    try {
                        return r.status === 200 && JSON.parse(r.body).items !== undefined;
                    } catch {
                        return false;
                    }
                },
            });

            if (!success) {
                featureFlagErrors.add(1);
            }
        } else {
            // Evaluate a random flag (simulate common keys)
            const flagKeys = ['test-flag', 'feature-a', 'feature-b', 'rollout-flag'];
            const randomKey = flagKeys[Math.floor(Math.random() * flagKeys.length)];
            
            const res = http.get(`${BASE_URL}/evaluation/${randomKey}`, {headers});
            
            featureFlagRequests.add(1);
            featureFlagDuration.add(res.timings.duration);

            const success = check(res, {
                'evaluation completed': (r) => r.status === 200 || r.status === 404,
            });

            if (!success) {
                featureFlagErrors.add(1);
            }
        }
    } else {
        // Write operations: Create, update, or delete
        const writeType = Math.random();
        
        if (writeType < 0.5) {
            // Create a feature flag
            const payload = JSON.stringify({
                key: `local-flag-${__VU}-${Date.now()}`,
                description: `Local test flag VU ${__VU}`,
                enabled: true,
                parameters: [
                    {
                        ruleType: 0,
                        ruleValue: '50'
                    }
                ]
            });

            const res = http.post(`${BASE_URL}/feature-flags`, payload, {headers});

            featureFlagRequests.add(1);
            featureFlagDuration.add(res.timings.duration);

            const success = check(res, {
                'create status is 200 or 201': (r) => r.status === 200 || r.status === 201,
            });

            if (!success) {
                featureFlagErrors.add(1);
            }

            // If successful, immediately update it
            if (res.status === 200 || res.status === 201) {
                const createdFlag = JSON.parse(res.body);
                const etag = res.headers['Etag'] || res.headers['etag'];

                const updatePayload = JSON.stringify({
                    key: createdFlag.key,
                    description: 'Updated in same iteration',
                    enabled: false,
                    parameters: createdFlag.parameters
                });

                const updateHeaders = {...headers};
                if (etag) {
                    updateHeaders['If-Match'] = etag;
                }

                const updateRes = http.patch(
                    `${BASE_URL}/feature-flags/${createdFlag.key}`,
                    updatePayload,
                    {headers: updateHeaders}
                );

                featureFlagRequests.add(1);
                featureFlagDuration.add(updateRes.timings.duration);

                const updateSuccess = check(updateRes, {
                    'update status is 200': (r) => r.status === 200,
                });

                if (!updateSuccess) {
                    featureFlagErrors.add(1);
                }
            }
        } else {
            // Create and immediately delete
            const payload = JSON.stringify({
                key: `temp-flag-${__VU}-${Date.now()}`,
                description: `Temporary flag VU ${__VU}`,
                enabled: true,
                parameters: []
            });

            const createRes = http.post(`${BASE_URL}/feature-flags`, payload, {headers});

            featureFlagRequests.add(1);
            featureFlagDuration.add(createRes.timings.duration);

            const createSuccess = check(createRes, {
                'create status is 200 or 201': (r) => r.status === 200 || r.status === 201,
            });

            if (!createSuccess) {
                featureFlagErrors.add(1);
            }

            if (createRes.status === 200 || createRes.status === 201) {
                const createdFlag = JSON.parse(createRes.body);

                const deleteRes = http.del(
                    `${BASE_URL}/feature-flags/${createdFlag.key}`,
                    null,
                    {headers}
                );

                featureFlagRequests.add(1);
                featureFlagDuration.add(deleteRes.timings.duration);

                const deleteSuccess = check(deleteRes, {
                    'delete status is 200 or 204': (r) => r.status === 200 || r.status === 204,
                });

                if (!deleteSuccess) {
                    featureFlagErrors.add(1);
                }
            }
        }
    }
}

export function teardown(data) {
    console.log('');
    console.log('=====================================');
    console.log('  Local Mixed Test Completed!       ');
    console.log('=====================================');
    console.log('');
    console.log('View detailed results:');
    console.log('  • Prometheus: http://localhost:9090');
    console.log('  • Grafana: http://localhost:3000');
    console.log('');
}
