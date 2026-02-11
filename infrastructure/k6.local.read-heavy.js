import http from 'k6/http';
import {check} from 'k6';
import {Counter, Rate, Trend} from 'k6/metrics';

// Custom metrics
const featureFlagRequests = new Counter('feature_flag_requests');
const featureFlagErrors = new Rate('feature_flag_errors');
const featureFlagDuration = new Trend('feature_flag_duration');

// Test configuration - Read-heavy workload for cache testing
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
    },
};

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
    console.log(`Running k6 read-heavy test against: ${BASE_URL}`);
    console.log(`Testing health endpoint first...`);

    const healthCheck = http.get(`${BASE_URL}/health`, {tags: {name: 'setup/health'}});
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
                userId: 'read-test-user',
                email: 'readtest@example.com',
                scopes: ['flags:read', 'flags:write', 'flags:delete'],
                role: 'admin'
            }),
            {
                headers: {'Content-Type': 'application/json'},
                tags: {name: 'setup/dev-token'},
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

    // 95% reads, 5% writes - read-heavy workload for cache stress testing
    const isReadOperation = Math.random() < 0.95;

    if (isReadOperation) {
        const readType = Math.random();
        
        if (readType < 0.5) {
            // Get all feature flags with pagination
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
            });

            if (!success) {
                recordResult(res, false);
            } else {
                recordResult(res, true);
            }
        } else if (readType < 0.8) {
            // Evaluate common flags (high cache hit rate)
            const flagKeys = ['test-flag', 'feature-a', 'feature-b', 'rollout-flag'];
            const randomKey = flagKeys[Math.floor(Math.random() * flagKeys.length)];
            
            const res = http.get(`${BASE_URL}/evaluation/${randomKey}`, {headers});

            const success = check(res, {
                'evaluation completed': (r) => r.status === 200 || r.status === 404,
            });

            recordResult(res, success);
        } else {
            // Get specific flag by key
            const flagKeys = ['test-flag', 'feature-a', 'feature-b'];
            const randomKey = flagKeys[Math.floor(Math.random() * flagKeys.length)];
            
            const res = http.get(`${BASE_URL}/feature-flags/${randomKey}`, {headers});

            const success = check(res, {
                'get by key completed': (r) => r.status === 200 || r.status === 404,
            });

            recordResult(res, success);
        }
    } else {
        // Minimal write operations to keep cache fresh
        const payload = JSON.stringify({
            key: `read-heavy-flag-${__VU}-${Date.now()}`,
            description: `Read-heavy test flag VU ${__VU}`,
            enabled: true,
            parameters: []
        });

        const res = http.post(`${BASE_URL}/feature-flags`, payload, {headers});

        const success = check(res, {
            'create status is 200 or 201': (r) => r.status === 200 || r.status === 201,
        });

        recordResult(res, success);
    }
}

export function teardown(data) {
    console.log('');
    console.log('=====================================');
    console.log('  Read-Heavy Test Completed!        ');
    console.log('=====================================');
    console.log('');
    console.log('View detailed results:');
    console.log('  • Prometheus: http://localhost:9090');
    console.log('  • Grafana: http://localhost:3000');
    console.log('');
}
