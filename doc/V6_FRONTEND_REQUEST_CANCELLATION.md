# V6 Frontend Request Cancellation

## Problem

The project task list reloads when the selected project, page, search text, or
filters change. Without cancellation, a slower request started for an older
query could finish after a newer request and replace the task list with stale
data.

## Request contract

For task-list reads:

1. Each `loadTasks` invocation creates an `AbortController`.
2. Starting a newer task-list request aborts the previous controller.
3. The `AbortSignal` is passed through `ProjectApi` to the shared `HttpClient`
   and then to `fetch`.
4. A response is applied only while its signal is still active.
5. Expected aborts do not clear the task list or display an error.
6. The request is also aborted when the task-loading effect is replaced or the
   provider unmounts.

The loading state is cleared only by the currently active request. An older
request cannot turn off the loading indicator for a newer request.

## Evidence

[`ProjectsContext.test.tsx`](../frontend/src/tests/context/ProjectsContext.test.tsx)
starts two task requests, changes the search state, and resolves the older
request after it has been aborted. The test verifies that:

- the first request signal is aborted;
- the second request remains active;
- the late old response does not replace the fresh task list;
- the loading state returns to idle after the current request completes.

## Boundaries and follow-up

- This slice covers the task-list read path in `ProjectsContext`.
- The existing `QuickSearchBar` already cancels its debounced workspace-search
  request; this change does not duplicate that behavior.
- Mutations such as create, update, delete, upload, and invitation operations
  are not automatically canceled because canceling a server-side command can
  leave an ambiguous outcome.
- Offline retry policy and broader cancellation UX remain separate scenarios.
