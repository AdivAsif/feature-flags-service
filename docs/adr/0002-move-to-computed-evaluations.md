# ADR 0002: Transition to Computed Evaluations (v1.1)

Date: 20-02-2026

## Status

Active

## Context

At 50,000 requests per second (RPS), the initial caching strategy (ADR 0001) resulted in massive cache churn. The Redis
instance was bombarded with unique `eval:{project}:{flag}:{user}` keys, causing high memory usage and potential
consistency issues, which led to a high latency spike, as well as unnecessary caching.

Benchmarking showed that while evaluation result caching was fast, the bottleneck under extreme load was often Garbage
Collection (GC) jitter and middleware overhead rather than the evaluation computation itself - this led to CPU spikes
and random latency spikes.

## Decision

I decided to move from **Result Caching** to **Flag Definition Caching**. This involves:

1. **Removing Evaluation Result Caching**: Commented out the `CachedEvaluationService` decorator. Evaluations are now
   always computed for every request.
2. **Relying on Definition Caching**: The system now relies on the `CachedFeatureFlagRepository` (which caches flag
   definitions). This metadata is much more stable and compact than individual evaluation results.
3. **Low-Allocation Context**: Refactored `EvaluationContext` from a `record` (class) to a `readonly struct` to
   eliminate heap allocations.
4. **Span-based Parsing**: Optimized the group-parsing logic in the evaluation endpoint to use `ReadOnlySpan<char>`,
   avoiding unnecessary `string` allocations and `ToUpperInvariant()` calls during the hot path.
5. **Optimized Auth**: Increased API key authentication cache from 1 minute to 10 minutes to reduce SHA256 hashing and
   repository lookup frequency.
6. **Middleware Bypass**: Configured `ETagMiddleware` to properly bypass evaluation endpoints entirely to save CPU
   cycles.

## Consequences

* **Pros**:
    * 100% Consistency: Evaluations always match the currently cached flag version.
    * Lower Memory: Eliminated the explosion of user-specific cache keys in Redis.
    * Stable Latency: Achieving a consistent p99 < 5 ms even at 50,000 RPS.
* **Cons**:
    * Slight Latency Increase: Small microsecond-level increase in latency compared to a pure cache hit, but well
      within the project's performance targets. Updated metrics are available in
      the [LocalHostBenchmarks file](../LocalHostBenchmarks.md)
