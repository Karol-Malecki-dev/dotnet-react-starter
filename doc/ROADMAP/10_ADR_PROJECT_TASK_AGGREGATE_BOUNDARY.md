# ADR: ProjectTask Aggregate Boundary

- Status: Accepted
- Date: 2026-08-29
- Scope: V3 decision about the relationship between `Project` and `ProjectTask`

## Context

The relational model contains a required `ProjectTask.ProjectId` foreign key, and the
HTTP endpoints for tasks are nested below a project. Neither fact determines an
aggregate boundary. The decision must follow the consistency rules and the way the
application loads and changes the data.

`Project` protects project ownership, membership and archival state. `ProjectTask`
has its own identifier, lifecycle and domain methods for title, description, status,
priority, assignment, due date and labels. Task commands do not load a `Project`
object and mutate it; they use a project access check and a task-specific persistence
port.

## Options considered

### Option 1: `ProjectTask` is part of the `Project` aggregate

`Project` would own a private task collection, and every task change would be made
through the `Project` aggregate root.

Benefits:

- Cross-task and project-task invariants could be enforced in one domain boundary.
- A change involving the project and its tasks could have one explicit consistency boundary.

Costs:

- Loading or modifying a project would be coupled to the size and write frequency of
  its task collection.
- Independent task edits would unnecessarily compete on the project concurrency token.
- Task labels, comments, attachments and reminders would make the aggregate large
  without a demonstrated invariant requiring all of them to be loaded together.

### Option 2: `ProjectTask` is a separate aggregate root

`ProjectTask` keeps `ProjectId` as an identity reference to the project. Application
services coordinate rules that need data from both aggregates.

Benefits:

- Project membership and task lifecycle have separate consistency boundaries.
- Task writes do not change the project aggregate version or require a project graph
  to be loaded.
- Query and command ports can remain focused on task use cases.

Costs:

- Rules involving both aggregates require an application or persistence query.
- A workflow that changes membership and task assignments must define its transaction
  boundary explicitly.
- A task-level concurrency token may be needed later if lost task edits become a
  material risk.

## Decision

`Project` and `ProjectTask` are separate aggregate roots.

The aggregate responsibilities are:

| Aggregate or boundary | Responsibilities |
| --- | --- |
| `Project` | Owner identity, archival state, project membership, member roles and invariants protecting the owner. |
| `ProjectTask` | Task identity and lifecycle, valid task state, normalized task data and labels. |
| Application service | Active-project access, role authorization, validation that an assignee is an active project member, and coordination of workflows crossing both aggregates. |
| Database mapping | Referential integrity through the `ProjectId` foreign key and delete behavior; a foreign key does not make the task a child entity of the `Project` aggregate. |

`ProjectTask.ProjectId` is therefore an identity reference, not a domain navigation
that requires the `Project` aggregate to be loaded. The task endpoints may remain
nested under `/api/projects/{projectId}/tasks` because that is an authorization and
resource-navigation choice, not an aggregate ownership statement.

Task labels remain inside the task consistency boundary because they are normalized,
deduplicated and replaced as part of task changes. This ADR does not reclassify task
comments, attachments or deadline reminders; their existing feature-specific ports
and independent lifecycle decisions remain unchanged.

The member-removal workflow is an application-level coordination case: it loads the
assigned tasks, unassigns them through `ProjectTask`, removes the member through
`Project`, and persists the changes together. The operation must keep an explicit
transaction boundary if it later grows to include additional writes or external side
effects.

## Implementation alignment

The current implementation follows this decision:

- `Project` exposes member behavior but no task collection or task mutation methods.
- `ProjectTask` has a private constructor, private state setters and task-specific
  domain methods.
- Task access checks the active project and loads the task using both `projectId` and
  `taskId`.
- Task commands use `IProjectTaskAccess` and `IProjectTaskCommandStore` rather than a
  project aggregate repository.
- `ProjectTaskConfiguration` maps the required relational foreign key without adding
  a `Project` navigation to the domain entity.
- `Project.ConcurrencyStamp` is not changed by task creation or task edits.

## Validation

The boundary is covered by
`IntegrationTests.ProjectTasksApiIntegrationTests.Project_task_changes_do_not_change_project_concurrency_stamp`.
The test creates and updates a task, then verifies that the project's concurrency
stamp remains unchanged.

Existing authorization, membership and archived-project tests remain the evidence for
the application-level rules that connect a task to its project.

## Follow-up decisions

- Add a `ProjectTask` concurrency token if task updates require lost-update detection.
- Revisit the classification of comments, attachments and reminders if their lifecycle
  or consistency rules become more independent.
- Define an explicit transaction or outbox policy if member removal gains notifications
  or other external side effects.
