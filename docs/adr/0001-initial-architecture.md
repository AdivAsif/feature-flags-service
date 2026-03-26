# 1: Initial Architecture

Date: 20-02-2026

## Status

 by [ADR 0002](0002-move-to-computed-evaluations.md)

## Context

The initial implementation of the feature flag service focused on minimizing latency for the evaluation hot-path. To
achieve this, several architectural choices were made:

1. **Evaluation Result Caching**: Evaluations were cached using a decorator pattern (`CachedEvaluationService`). This
   cached the final `EvaluationResponse` for a specific `(projectId, flagKey, userId, flagVersion, contextHash)`.
2. **FusionCache Integration**: Used for L1 (memory) and L2 (Redis) caching to provide extremely low latency (< 1 ms) for
   repeat requests.
3. **Record-based Models**: `EvaluationContext` was implemented as a `record` (class-based), which was convenient for
   immutability but resulted in heap allocations per request.
4. **Middleware Overhead**: Standard ASP.NET Core middleware (ETag, etc.) was active on all paths, including
   evaluations.
5. **Short Auth Cache**: API key authentication results were cached for only 1 minute.

## Decision

I implemented a multi-layered caching strategy where both flag definitions and evaluation results were cached. The
evaluation result cache was the primary source for the `/evaluation` endpoint.

## Consequences

* **Pros**: Extremely low latency for repeat evaluations of the same user/flag.
* **Cons**:
    * Significant cache churn in Redis due to user-specific keys unnecessarily.
    * Risk of stale results if many flags or users existed (consistency tied to TTL).
    * GC pressure at high RPS due to model allocations.
