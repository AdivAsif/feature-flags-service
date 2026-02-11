# ETag Implementation

This document describes the ETag (Entity Tag) implementation in the Feature Flags Service.

## Overview

ETags are HTTP response headers that represent a specific version of a resource. They enable efficient caching and
optimistic concurrency control by allowing clients to:

- **Avoid unnecessary data transfers** (304 Not Modified)
- **Prevent lost updates** (412 Precondition Failed)

## Implementation Details

### Components

1. **ETagMiddleware** (`Web.Api/Middleware/ETagMiddleware.cs`)
    - Global middleware that automatically adds ETags to all GET responses (aside from the hot-path)
    - Handles `If-None-Match` headers for conditional GET requests
    - Returns `304 Not Modified` when content hasn't changed

2. **ETagExtensions** (`Web.Api/Extensions/ETagExtensions.cs`)
    - Extension methods for generating ETags based on feature flag version
    - Methods for validating `If-Match` headers
    - ETags are computed using SHA256 hash of `{Id}-{Version}` (using SHA256 is okay here since it is purely for writes)

3. **Endpoints**:
    - `GetByKey`: Returns ETag header, supports If-None-Match
    - `GetAll`: Returns ETag based on collection hash, supports If-None-Match
    - `Update`: Validates If-Match header for optimistic concurrency

### ETag Format

ETags are generated using SHA256 hashing and follow the HTTP standard format:

```
ETag: "base64-encoded-sha256-hash"
```

For individual feature flags: Hash of `{Id}-{Version}`
For collections: Hash of a serialized collection

## Usage Examples

### 1. Conditional GET (Caching)

**First Request:**

```http
GET /feature-flags/my-feature
Authorization: Bearer {token}
```

**Response:**

```http
200 OK
ETag: "abc..."
Content-Type: application/json

{
  "id": "guid",
  "version": 1,
  "key": "my-feature",
  ...
}
```

**Subsequent Request:**

```http
GET /feature-flags/my-feature
Authorization: Bearer {token}
If-None-Match: "abc..."
```

**Response (if not modified):**

```http
304 Not Modified
ETag: "abc..."
```

### 2. Optimistic Concurrency Control

**Get current version:**

```http
GET /feature-flags/my-feature
Authorization: Bearer {token}
```

**Response:**

```http
200 OK
ETag: "abc..."
```

**Update with ETag validation:**

```http
PATCH /feature-flags/my-feature
Authorization: Bearer {token}
If-Match: "abc..."
Content-Type: application/json

{
  "enabled": true,
  "description": "Updated description"
}
```

**Success Response:**

```http
200 OK
ETag: "xyz..."
```

**Conflict Response (if resource changed):**

```http
412 Precondition Failed
```

## Reasoning

1. Bandwidth Optimization: Clients can avoid downloading unchanged resources
2. Concurrency Safety: Prevents lost updates when multiple clients modify the same resource
4. RFC 7232 compliant

## Version Field

The implementation leverages the existing `Version` field in the `FeatureFlag` entity, which is automatically
incremented on each update. This just makes concurrency control more concrete, both on the HTTP side and the
database side.

## Notes

- ETags are always needed for the Update endpoint when `If-Match` header is present
- ETags aren't necessary for GET requests (clients can choose to use them or not)
- The middleware handles ETag generation for all GET responses automatically aside from evaluation