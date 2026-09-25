# V6 Frontend Request Coordination

## Problem

The frontend uses one shared `HttpClient` for protected API calls. Before this
slice, every protected request that received `401 Unauthorized` could invoke
the refresh handler independently. When several requests expired at the same
time, that created concurrent refresh calls and several retries using different
refresh-token rotation outcomes.

## Request contract

For a protected request:

1. The client captures the access token used for the original request.
2. A `401` caused by a token that was already replaced by another request is
   retried once with the current token without starting another refresh.
3. Otherwise, concurrent unauthorized requests share one in-flight refresh
   promise.
4. Every request is retried at most once after a successful refresh.
5. If refresh fails, the original `HttpError` and the existing session-expired
   notice behavior remain visible to the caller.

Requests marked with `skipAuth` never start refresh handling. This is required
for public operations such as login, where an invalid credential response must
not refresh an already existing session.

## Implementation

`HttpClient` owns the in-flight refresh promise. The application-level
`AppNoticeCenter` continues to provide the refresh handler, so token persistence
and authentication state remain owned by `AuthContext` and `TokenManager`.

The coordination is scoped to one `HttpClient` instance. The application uses
the shared `httpClient` singleton, so all API services participate in the same
single-flight boundary.

## Evidence

[`HttpClient.test.ts`](../frontend/src/tests/services/api/HttpClient.test.ts)
verifies that:

- two concurrent `401` responses invoke the refresh handler once and both
  requests retry with the new token;
- a request whose token was replaced by another request retries without a
  second refresh;
- `skipAuth` requests do not invoke authenticated-session refresh handling.

## Boundaries and follow-up

- This slice does not automatically retry network errors or `5xx` responses.
- It does not add a global retry budget, exponential backoff, or circuit breaker.
- Request cancellation still follows the caller-provided `AbortSignal`.
- The application now exposes browser online/offline state through
  `NetworkStatusBanner`, while task-list failures expose an explicit retry
  action. This is a user-driven recovery path, not an automatic retry policy.
