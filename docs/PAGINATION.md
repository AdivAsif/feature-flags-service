# Cursor-Based Pagination

This document explains the cursor-based pagination implementation in this solution.

## Overview

Cursor-based pagination is a pagination technique that uses a pointer or a cursor in this case to
navigate through large datasets. Unlike offset-based pagination, cursor pagination provides:

- Better performance (generally) - No skipping rows, directly queries from the cursor position
- Scalability - Performance doesn't degrade with large offsets and datasets
- Real-time friendly - Handles concurrent inserts/deletes more gracefully

## Why Cursor Pagination Over Offset/Limit?

### Problems with Offset/Limit:

1. Inconsistent results - Items can appear twice or be missed if data changes between requests
2. Deep pagination is slower - `OFFSET 10000 LIMIT 10` reads and discards 10,000 rows

### Benefits of Cursor Pagination:

1. Queries use indexed columns with direct positioning
2. Each cursor represents a unique position in the dataset
3. New items don't affect current pagination state
4. Works better with millions of records

## Implementation Details

### Cursor Structure

Cursors are **base64-encoded strings** containing:

- Resource ID (GUID)
- CreatedAt timestamp

### Query Parameters

| Parameter | Type   | Default | Description                       |
|-----------|--------|---------|-----------------------------------|
| `first`   | int    | 10      | Number of items to return (1-100) |
| `after`   | string | null    | Cursor to fetch items after       |
| `before`  | string | null    | Cursor to fetch items before      |

### Response Structure

```json
{
  "items": [
    {
      "id": "guid",
      "key": "feature-key",
      "version": 1,
      ...
    }
  ],
  "pageInfo": {
    "hasNextPage": true,
    "hasPreviousPage": false,
    "startCursor": "encoded-cursor",
    "endCursor": "encoded-cursor",
    "totalCount": 42
  }
}
```

### PageInfo Fields

- `hasNextPage` - True if more items exist after the current page
- `hasPreviousPage` - True if items exist before the current page
- `startCursor` - Cursor of the first item in the current page
- `endCursor` - Cursor of the last item in the current page
- `totalCount` - Total number of items in the entire collection

## Usage Examples

### 1. First Page (Initial Request)

**Request:**

```http
GET /feature-flags?first=10 HTTP/1.1
Authorization: Bearer {token}
```

**Response:**

```json
{
  "items": [
    ...10 items...
  ],
  "pageInfo": {
    "hasNextPage": true,
    "hasPreviousPage": false,
    "startCursor": "abc...",
    "endCursor": "def...",
    "totalCount": 42
  }
}
```

### 2. Next Page (Forward Pagination)

Use the `endCursor` from the previous response with the `after` parameter:

**Request:**

```http
GET /feature-flags?first=10&after=def... HTTP/1.1
Authorization: Bearer {token}
```

**Response:**

```json
{
  "items": [
    ...10 more items...
  ],
  "pageInfo": {
    "hasNextPage": true,
    "hasPreviousPage": true,
    "startCursor": "ghi...",
    "endCursor": "jkl...",
    "totalCount": 42
  }
}
```

### 3. Previous Page (Backward Pagination)

Use the `startCursor` from the current response with the `before` parameter:

**Request:**

```http
GET /feature-flags?first=10&before=def...
Authorization: Bearer {token} (or API key)
```

**Response:**

```json
{
  "items": [
    ...10 previous items...
  ],
  "pageInfo": {
    "hasNextPage": true,
    "hasPreviousPage": false,
    "startCursor": "abc...",
    "endCursor": "xyz...",
    "totalCount": 42
  }
}
```

### 4. Custom Page Size

**Request:**

```http
GET /feature-flags?first=25
Authorization: Bearer {token} (or API key)
```

Returns up to 25 items (clamped between 1-100).

## Ordering

Items are ordered by:

1. `CreatedAt` (ascending) - Primary sort key
2. `Id` (ascending) - Secondary sort key

This ensures:

- Deterministic ordering
- Unique positioning for each item
- Stable pagination across requests

## Implementation Architecture

### Layers

1. **Contracts Layer**
    - `PagedResult<T>` - Generic paged response wrapper
    - `Items` - Collection of items to return as an IReadOnlyList<T>
    - `PageInfo` - Pagination metadata

2. **Infrastructure Layer**
    - `FeatureFlagsRepository.GetPagedAsync()` - Database query with cursor logic (also applies to other repositories)
    - `CachedRepository` decorator - Caching layer with pagination support
    - `CursorHelper` - Cursor encoding/decoding utilities

3. **Application Layer**
    - `IFeatureFlagsService.GetPagedAsync()` - Business logic interface
    - `FeatureFlagsService` - Maps domain entities to responses
    - `Slice<T>` - Cursor-based pagination slice

4. **Web.Api Layer**
    - `GetAll` endpoint - HTTP interface with query parameters

## Database Query Optimization

The implementation uses indexed queries for efficient cursor positioning:

**Note:** The query fetches `first + 1` items to determine if more pages exist.

## Best Practices

### For API/SDK Consumers

1. Keep `endCursor` for next page, `startCursor` for previous page
2. Treat cursors as opaque strings - no need to decode or validate
3. Check `hasNextPage` before requesting next page
4. Recommended pagination size is between 10-50
5. Use `before` and `after` parameters exclusively (don't mix)

## Error Handling

### Invalid Cursor

If a cursor is incorrect or invalid:

- Returns the first page by default

### Cursor Points to Deleted Item

If the cursor references a deleted item:

- Continues from the next available position
- Graceful degradation, no errors thrown

### Empty Results

```json
{
  "items": [],
  "pageInfo": {
    "hasNextPage": false,
    "hasPreviousPage": false,
    "startCursor": null,
    "endCursor": null,
    "totalCount": 0
  }
}
```

## GraphQL Relay Specification

This implementation follows
the [GraphQL Cursor Connections Specification (Relay)](https://relay.dev/graphql/connections.htm)
which is a well-established standard for cursor-based pagination, following their naming standards and conventions.

## Future Enhancements

Potential improvements for production:

1. **Cursor expiration** - Add timestamps to cursors and validate age
2. **Filtering support** - Combine cursors with search/filter parameters
3. **Sorting options** - Allow different sort orders (by key, updated date, etc.)
4. **Cursor encryption** - Encrypt cursors to prevent tampering
5. **Bidirectional iteration** - More sophisticated previous page handling
6. **Pattern-based cache invalidation** - For more efficient cache clearing

## References

- [GraphQL Cursor Connections Specification](https://relay.dev/graphql/connections.htm) - official specification
- [Use the Index, Luke: No Offset](https://use-the-index-luke.com/no-offset) - explains why offset pagination is worse
- [Keyset Pagination](https://www.postgresql.org/docs/current/queries-limit.html) - explains why large offsets are
  inefficient
