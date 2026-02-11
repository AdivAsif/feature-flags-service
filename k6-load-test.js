import http from 'k6/http';
import {check} from 'k6';
import {Counter, Rate, Trend} from 'k6/metrics';

// Custom metrics
const featureFlagRequests = new Counter('feature_flag_requests');
const featureFlagErrors = new Rate('feature_flag_errors');
const featureFlagDuration = new Trend('feature_flag_duration');

// Test configuration
export const options = {
    stages: [
        {duration: '30s', target: 10},  // Ramp up to 10 users
        {duration: '1m', target: 50},   // Ramp up to 50 users
        {duration: '2m', target: 100},  // Ramp up to 100 users
        {duration: '2m', target: 100},  // Stay at 100 users
        {duration: '1m', target: 200},  // Spike to 200 users
        {duration: '1m', target: 200},  // Stay at peak
        {duration: '30s', target: 0},   // Ramp down to 0 users
    ],
    thresholds: {
        http_req_duration: ['p(95)<10', 'p(99)<15'], // 95% under 10ms, 99% under 15ms
        http_req_failed: ['rate<0.05'],                  // Error rate under 5%
        feature_flag_errors: ['rate<0.05'],
        feature_flag_duration: ['p(95)<10'],
    },
};

// Use host.docker.internal for Docker network or localhost for local runs
const BASE_URL = __ENV.BASE_URL || 'http://host.docker.internal:5000';
const TOKEN = __ENV.AUTH_TOKEN || '';

function recordResult(res, success) {
    featureFlagRequests.add(1);
    featureFlagErrors.add(!success);

    const duration = res?.timings?.duration;
    if (Number.isFinite(duration) && duration >= 0) {
        featureFlagDuration.add(duration);
    }
}

// Helper function to get auth headers
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
    console.log(`Running k6 load test against: ${BASE_URL}`);
    console.log(`Testing health endpoint first...`);

    // Test basic connectivity
    const healthCheck = http.get(`${BASE_URL}/health`, {tags: {name: 'setup/health'}});
    console.log(`Health check status: ${healthCheck.status}`);
    console.log(`Health check body: ${healthCheck.body}`);

    if (healthCheck.status !== 200) {
        console.error(`API health check failed. Status: ${healthCheck.status}`);
        console.error(`Response: ${healthCheck.body}`);
        throw new Error('API is not healthy');
    }

    // Get or create an auth token for testing
    if (!TOKEN) {
        console.log('Attempting to get dev token...');
        const devTokenResponse = http.post(
            `${BASE_URL}/dev/token`,
            JSON.stringify({
                userId: 'load-test-user',
                email: 'loadtest@example.com',
                scopes: ['flags:read', 'flags:write', 'flags:delete'],
                role: 'admin'
            }),
            {
                headers: {'Content-Type': 'application/json'},
                tags: {name: 'setup/dev-token'},
            }
        );

        console.log(`Dev token response status: ${devTokenResponse.status}`);

        if (devTokenResponse.status === 200) {
            const body = JSON.parse(devTokenResponse.body);
            console.log('Auth token obtained successfully');
            return {token: body.token};
        } else {
            console.log(`Failed to get auth token. Status: ${devTokenResponse.status}`);
            console.log(`Response: ${devTokenResponse.body}`);
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

    // Scenario 1: Get all feature flags (with pagination)
    {
        const res = http.get(`${BASE_URL}/feature-flags?first=20`, {headers});

        const success = check(res, {
            'status is 200': (r) => r.status === 200,
            'has items': (r) => {
                try {
                    return r.status === 200 && JSON.parse(r.body).items !== undefined;
                } catch {
                    return false;
                }
            },
            'has pageInfo': (r) => {
                try {
                    return r.status === 200 && JSON.parse(r.body).pageInfo !== undefined;
                } catch {
                    return false;
                }
            },
        });

        recordResult(res, success);
    }

    // Scenario 2: Create a feature flag
    {
        const payload = JSON.stringify({
            key: `load-test-flag-${__VU}-${Date.now()}`,
            description: `Load test feature flag created by VU ${__VU}`,
            enabled: true,
            parameters: [
                {
                    ruleType: 0,
                    ruleValue: '50'
                }
            ]
        });

        const res = http.post(`${BASE_URL}/feature-flags`, payload, {headers});

        const success = check(res, {
            'create status is 200 or 201': (r) => r.status === 200 || r.status === 201,
            'has created flag': (r) => {
                try {
                    if (r.status !== 200 && r.status !== 201) return false;
                    const body = JSON.parse(r.body);
                    return body.key !== undefined;
                } catch (e) {
                    console.error(`Failed to parse create response: ${e.message}, body: ${r.body.substring(0, 100)}`);
                    return false;
                }
            },
        });

        recordResult(res, success);

        // If successful, try to get, evaluate, update and delete it
        if (res.status === 200 || res.status === 201) {
            const createdFlag = JSON.parse(res.body);
            const etag = res.headers['Etag'] || res.headers['etag'];

            // Scenario 3: Get the feature flag by key
            const getRes = http.get(
                `${BASE_URL}/feature-flags/${createdFlag.key}`,
                {headers}
            );

            const getSuccess = check(getRes, {
                'get by key status is 200': (r) => r.status === 200,
                'get by key returns flag': (r) => {
                    try {
                        if (r.status !== 200) return false;
                        const body = JSON.parse(r.body);
                        return body.key === createdFlag.key;
                    } catch {
                        return false;
                    }
                },
                'get by key has etag': (r) => {
                    const etag = r.headers['Etag'] || r.headers['etag'];
                    return etag !== undefined && etag !== '';
                }
            });

            recordResult(getRes, getSuccess);

            const getEtag = getRes.headers['Etag'] || getRes.headers['etag'];
            if (getEtag) {
                const conditionalRes = http.get(
                    `${BASE_URL}/feature-flags/${createdFlag.key}`,
                    {headers: {...headers, 'If-None-Match': getEtag}}
                );

                const conditionalSuccess = check(conditionalRes, {
                    'conditional get is 304 or 200': (r) => r.status === 304 || r.status === 200,
                });

                recordResult(conditionalRes, conditionalSuccess);
            }

            // Scenario 4: Evaluate the feature flag
            const evalRes = http.get(
                `${BASE_URL}/evaluation/${createdFlag.key}`,
                {headers}
            );

            const evalSuccess = check(evalRes, {
                'evaluation status is 200': (r) => r.status === 200,
                'evaluation has result': (r) => {
                    try {
                        if (r.status !== 200) return false;
                        const body = JSON.parse(r.body);
                        return body.allowed !== undefined;
                    } catch {
                        return false;
                    }
                }
            });

            recordResult(evalRes, evalSuccess);

            // Scenario 5: Update the feature flag
            const updatePayload = JSON.stringify({
                key: createdFlag.key,
                description: 'Updated description',
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

            const updateSuccess = check(updateRes, {
                'update status is 200': (r) => r.status === 200,
            });

            recordResult(updateRes, updateSuccess);

            // Scenario 6: Delete the feature flag
            const deleteRes = http.del(
                `${BASE_URL}/feature-flags/${createdFlag.key}`,
                null,
                {headers}
            );

            const deleteSuccess = check(deleteRes, {
                'delete status is 200 or 204': (r) => r.status === 200 || r.status === 204,
            });

            recordResult(deleteRes, deleteSuccess);
        }
    }
}

export function teardown(data) {
    console.log('');
    console.log('=====================================');
    console.log('  Load Test Completed Successfully!  ');
    console.log('=====================================');
    console.log('');
    console.log('View detailed results:');
    console.log('  • Prometheus: http://localhost:9090');
    console.log('  • Grafana: http://localhost:3000');
    console.log('');
}
