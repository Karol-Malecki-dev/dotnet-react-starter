# V4 Product Event Matrix

This matrix records the release-level decision for critical collaboration events.
Security audit events remain separate from product activity and user notifications.

| Trigger | Product activity | Recipient notification | Security audit | Atomic with state change | Resource link | Deduplication key | Tests |
|---|---|---|---|---|---|---|---|
| Project create | Existing | Not required for owner | Not applicable | Yes | Project | Project id + event | Project API tests |
| Project archive | Existing | Not required for actor | Not applicable | Yes | Project | Project id + event | Project API tests |
| Invitation create | Existing | Existing | Not applicable | Yes | Invitation | Invitation id + event | Invitation integration tests |
| Invitation accept/decline | Existing | Existing behavior verified | Not applicable | Yes | Project | Invitation id + decision | Invitation integration tests |
| Member add/remove/role change | Existing | Existing behavior verified | Not applicable | Yes | Project members | Project + user + event | Member integration tests |
| Task create/update/status/delete | Existing | Assignment notification supported | Not applicable | Yes | Task | Task id + event + version | Task integration tests |
| Comment add | Existing | Existing behavior verified | Not applicable | Yes | Task | Comment id + event | Comment integration tests |
| Attachment add/delete | Existing | Existing behavior verified | Not applicable | Yes | Attachment | Attachment id + event | Attachment integration tests |
| Deadline approaching/overdue | Existing processor | Existing reminder flow | Not applicable | Processor transaction | Task | Task id + reminder type | Reminder tests |
| Administrator role change | Not applicable | Not applicable | `account.role.changed` | Yes | Admin user | Subject + event + occurrence | Admin controller tests |
| Administrator account status change | Not applicable | Not applicable | `account.status.changed` | Yes | Admin user | Subject + event + occurrence | Admin controller tests |

## Release decision

The existing activity and notification implementations are retained for V4. Missing
recipient-specific collaboration notifications are documented as follow-up work rather
than being added without a stable recipient and deduplication contract.
