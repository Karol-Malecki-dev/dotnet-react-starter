# V4 Product Event Matrix

This matrix records the release-level decision for critical collaboration events.
Security audit events remain separate from product activity and user notifications.

| Trigger | Product activity | Recipient notification | Security audit | Atomic with state change | Resource link | Deduplication key | Tests |
|---|---|---|---|---|---|---|---|
| Project create | Existing | None; the owner is the actor | Not applicable | Yes | Project | Not applicable | Project API tests |
| Project archive | Existing | None; the owner is the actor | Not applicable | Yes | Project | Not applicable | Project API tests |
| Invitation create | Existing | Invited user | Not applicable | Yes | Invitation | Deferred for the legacy invitation writer | Invitation unit and integration tests |
| Invitation accept/decline | Existing | Project owner | Not applicable | Yes | Invitation | Deferred for the legacy invitation writer | Invitation response unit and integration tests |
| Direct member add | Existing | Added user | Not applicable | Yes | Project | Deferred for the legacy member writer | Member integration tests |
| Member remove | Existing | Removed user | Not applicable | Yes | Project | `project:{projectId}:member:{userId}:removed` | Member and collaboration notification unit tests |
| Member role change | Existing | Affected member | Not applicable | Yes | Project | `project:{projectId}:member:{userId}:role:{role}` | Member and collaboration notification unit tests |
| Task create or reassignment | Existing | Newly assigned user, excluding the actor | Not applicable | Yes | Task | Deferred for the legacy assignment writer | Task and assignment notification unit tests |
| Task update/delete | Existing | None beyond reassignment | Not applicable | Yes | Task | Not applicable | Task integration tests |
| Task status change | Existing on a real change | Assigned user, excluding the actor | Not applicable | Yes | Task | `task:{taskId}:status:{status}:version:{stamp}` | Status and collaboration notification unit tests |
| Comment add | Existing | Assigned user, excluding the author | Not applicable | Yes | Task | `task:{taskId}:comment:{commentId}` | Comment persistence and collaboration notification unit tests |
| Attachment add | Existing | Assigned user, excluding the uploader | Not applicable | Metadata transaction; binary cleanup compensates on failure | Task | `task:{taskId}:attachment:{attachmentId}:added` | Attachment handler and collaboration notification unit tests |
| Attachment delete | Existing | Assigned user, excluding the actor | Not applicable | Metadata, activity, cleanup message, and notification are atomic | Task | `task:{taskId}:attachment:{attachmentId}:removed` | Attachment cleanup and collaboration notification unit tests |
| Deadline approaching/overdue | Existing processor | Assigned user | Not applicable | Processor transaction | Task | Existing reminder uniqueness contract | Reminder tests |
| Administrator role change | Not applicable | Not applicable | `account.role.changed` | Yes | Admin user | Subject + event + occurrence | Admin controller tests |
| Administrator account status change | Not applicable | Not applicable | `account.status.changed` | Yes | Admin user | Subject + event + occurrence | Admin controller tests |

## Release decision

V4 adds recipient-specific notifications for member removal, member role changes, task
status changes, comments, and attachment changes. These notifications share the
`ICollaborationNotificationWriter` boundary and a database-enforced per-user
deduplication key. Notifications and optional email outbox records are staged in the
same scoped `ApplicationDbContext` as the state change.

Invitation, direct-member-add, and task-assignment writers predate the shared
deduplication boundary. Their behavior and transaction ordering are retained, but
migrating them to `ICollaborationNotificationWriter` is deferred to avoid changing
already stable flows during the V4 release gate. Project create/archive and ordinary
task update/delete intentionally do not notify the actor.
