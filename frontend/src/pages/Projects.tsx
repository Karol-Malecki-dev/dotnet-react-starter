import { useEffect, useMemo, useState } from 'react';
import {
  ProjectTaskPriority,
  ProjectTaskStatus,
  ProjectMemberRole,
  ProjectInvitationStatus,
  type CreateProjectTaskRequest,
  type ProjectDto,
  type ProjectMemberDto,
  type ProjectTaskDto,
} from '../types';
import { useProjects } from '../context/ProjectsContext';
import { useFeatureAvailability } from '../hooks/useFeatureAvailability';
import { useAuth } from '../hooks/useAuth';

const statusLabels: Record<ProjectTaskStatus, string> = {
  [ProjectTaskStatus.Todo]: 'To do',
  [ProjectTaskStatus.InProgress]: 'In progress',
  [ProjectTaskStatus.Done]: 'Done',
};

const priorityLabels: Record<ProjectTaskPriority, string> = {
  [ProjectTaskPriority.Low]: 'Low',
  [ProjectTaskPriority.Normal]: 'Normal',
  [ProjectTaskPriority.High]: 'High',
};

function ProjectForm({ project, onSubmit, onCancel }: {
  project?: ProjectDto;
  onSubmit: (name: string, description: string) => Promise<void>;
  onCancel?: () => void;
}) {
  const [name, setName] = useState(project?.name ?? '');
  const [description, setDescription] = useState(project?.description ?? '');
  const [saving, setSaving] = useState(false);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!name.trim()) return;
    setSaving(true);
    try {
      await onSubmit(name.trim(), description.trim());
      if (!project) {
        setName('');
        setDescription('');
      }
    } finally {
      setSaving(false);
    }
  };

  return (
    <form className="form" onSubmit={submit}>
      <div className="field"><label className="field__label" htmlFor="project-name">Name</label><input id="project-name" value={name} onChange={(event) => setName(event.target.value)} required /></div>
      <div className="field"><label className="field__label" htmlFor="project-description">Description</label><textarea id="project-description" value={description} onChange={(event) => setDescription(event.target.value)} rows={3} /></div>
      <div className="hero__actions">
        <button className="button" type="submit" disabled={saving}>{saving ? 'Saving...' : project ? 'Save changes' : 'Create project'}</button>
        {onCancel ? <button className="button button--ghost" type="button" onClick={onCancel}>Cancel</button> : null}
      </div>
    </form>
  );
}

function TaskForm({ onSubmit, initialTask, assignmentEnabled, members }: { onSubmit: (request: CreateProjectTaskRequest) => Promise<unknown>; initialTask?: ProjectTaskDto; assignmentEnabled?: boolean; members: ProjectMemberDto[] }) {
  const [title, setTitle] = useState(initialTask?.title ?? '');
  const [description, setDescription] = useState(initialTask?.description ?? '');
  const [priority, setPriority] = useState(initialTask?.priority ?? ProjectTaskPriority.Normal);
  const [dueDate, setDueDate] = useState(initialTask?.dueDate?.slice(0, 10) ?? '');
  const [assignedUserId, setAssignedUserId] = useState(initialTask?.assignedUserId ?? '');
  const [saving, setSaving] = useState(false);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!title.trim()) return;
    setSaving(true);
    try {
      await onSubmit({ title: title.trim(), description: description.trim() || undefined, priority, dueDate: dueDate || undefined, assignedUserId: assignedUserId || undefined });
      if (!initialTask) {
        setTitle('');
        setDescription('');
        setDueDate('');
        setAssignedUserId('');
      }
    } finally {
      setSaving(false);
    }
  };

  return (
    <form className="form" onSubmit={submit}>
      <div className="field"><label className="field__label" htmlFor="task-title">Task title</label><input id="task-title" value={title} onChange={(event) => setTitle(event.target.value)} required /></div>
      <div className="field"><label className="field__label" htmlFor="task-description">Description</label><textarea id="task-description" value={description} onChange={(event) => setDescription(event.target.value)} rows={2} /></div>
      <div className="grid grid--2">
        <div className="field"><label className="field__label" htmlFor="task-priority">Priority</label><select id="task-priority" value={priority} onChange={(event) => setPriority(Number(event.target.value) as ProjectTaskPriority)}>{Object.entries(priorityLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></div>
        <div className="field"><label className="field__label" htmlFor="task-due-date">Due date</label><input id="task-due-date" type="date" value={dueDate} onChange={(event) => setDueDate(event.target.value)} /></div>
      </div>
      {assignmentEnabled ? <div className="field"><label className="field__label" htmlFor="task-assignee">Assign to</label><select id="task-assignee" value={assignedUserId} onChange={(event) => setAssignedUserId(event.target.value)}><option value="">Unassigned</option>{members.map((member) => <option key={member.userId} value={member.userId}>{member.displayName} ({member.email})</option>)}</select></div> : null}
      <button className="button" type="submit" disabled={saving}>{saving ? initialTask ? 'Saving...' : 'Adding...' : initialTask ? 'Save changes' : 'Add task'}</button>
    </form>
  );
}

function MemberPanel({ members, availableMembers, ownerId, onAdd, onRemove, onRoleChange }: { members: ProjectMemberDto[]; availableMembers: { id: string; displayName: string; email: string }[]; ownerId: string; onAdd: (userId: string) => Promise<void>; onRemove: (userId: string) => Promise<void>; onRoleChange?: (userId: string, role: ProjectMemberRole) => Promise<void> }) {
  const [selectedUserId, setSelectedUserId] = useState('');
  const [saving, setSaving] = useState(false);

  const add = async () => {
    if (!selectedUserId) return;
    setSaving(true);
    try {
      await onAdd(selectedUserId);
      setSelectedUserId('');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="card">
      <h2>Project members</h2>
      <div className="member-list">
        {members.map((member) => (
          <div className="member-list__item" key={member.userId}>
            <span><strong>{member.displayName}</strong><small>{member.email}</small></span>
            {member.userId !== ownerId ? <><select aria-label={`Role for ${member.displayName}`} value={member.role ?? ProjectMemberRole.Member} onChange={(event) => { if (onRoleChange) void onRoleChange(member.userId, Number(event.target.value) as ProjectMemberRole); }}><option value={ProjectMemberRole.Member}>Member</option><option value={ProjectMemberRole.Viewer}>Viewer</option></select><button className="button button--danger" type="button" onClick={() => void onRemove(member.userId)}>Remove</button></> : <span className="role-badge">Owner</span>}
          </div>
        ))}
      </div>
      <div className="member-add">
        <select aria-label="Available project users" value={selectedUserId} onChange={(event) => setSelectedUserId(event.target.value)}>
          <option value="">Select user to add</option>
          {availableMembers.map((user) => <option key={user.id} value={user.id}>{user.displayName} ({user.email})</option>)}
        </select>
        <button className="button" type="button" disabled={!selectedUserId || saving} onClick={() => void add()}>{saving ? 'Adding...' : 'Add member'}</button>
      </div>
    </div>
  );
}

function InvitationPanel({ projectId, invitations, loading, onLoad, onCreate }: { projectId: string; invitations: { id: string; invitedUserDisplayName: string; invitedUserEmail: string; role: ProjectMemberRole; status: ProjectInvitationStatus; expiresAt: string }[]; loading: boolean; onLoad: (projectId: string) => Promise<void>; onCreate: (request: { email: string; role: ProjectMemberRole }) => Promise<{ token: string }> }) {
  const [email, setEmail] = useState('');
  const [role, setRole] = useState(ProjectMemberRole.Member);
  const [inviteLink, setInviteLink] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => { void onLoad(projectId); }, [onLoad, projectId]);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!email.trim()) return;
    setSaving(true);
    try {
      const created = await onCreate({ email: email.trim(), role });
      setInviteLink(`${window.location.origin}/project-invitation?token=${encodeURIComponent(created.token)}`);
      setEmail('');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="card invitation-panel">
      <div><h2>Invite to project</h2><p className="page-note">Create a time-limited invitation for an existing account.</p></div>
      <form className="invitation-panel__form" onSubmit={submit}>
        <div className="field"><label className="field__label" htmlFor="invitation-email">Account email</label><input id="invitation-email" type="email" value={email} onChange={(event) => setEmail(event.target.value)} required /></div>
        <div className="field"><label className="field__label" htmlFor="invitation-role">Role</label><select id="invitation-role" value={role} onChange={(event) => setRole(Number(event.target.value) as ProjectMemberRole)}><option value={ProjectMemberRole.Member}>Member</option><option value={ProjectMemberRole.Viewer}>Viewer</option></select></div>
        <button className="button" type="submit" disabled={saving}>{saving ? 'Creating...' : 'Create invitation'}</button>
      </form>
      {inviteLink ? <div className="invitation-panel__link"><label className="field__label" htmlFor="invitation-link">Invitation link</label><input id="invitation-link" value={inviteLink} readOnly /><button className="button button--ghost" type="button" onClick={() => void navigator.clipboard.writeText(inviteLink)}>Copy link</button></div> : null}
      <div><h3>Invitation history</h3>{loading ? <p className="page-note">Loading invitations...</p> : invitations.length === 0 ? <p className="page-note">No invitations sent yet.</p> : <div className="member-list">{invitations.map((invitation) => <div key={invitation.id} className="member-list__item"><span><strong>{invitation.invitedUserDisplayName}</strong><small>{invitation.invitedUserEmail}</small></span><small>{invitation.role === ProjectMemberRole.Member ? 'Member' : 'Viewer'} · {ProjectInvitationStatus[invitation.status]} · expires {new Date(invitation.expiresAt).toLocaleDateString()}</small></div>)}</div>}</div>
    </div>
  );
}

function TaskItem({ task, canManage, discussionOpen, onToggleDiscussion, onStatusChange, onDelete, onEdit }: { task: ProjectTaskDto; canManage: boolean; discussionOpen: boolean; onToggleDiscussion: () => void; onStatusChange: (status: ProjectTaskStatus) => Promise<void>; onDelete: () => Promise<void>; onEdit: () => void }) {
  const [deleting, setDeleting] = useState(false);
  return (
    <article className="task-item">
      <div><h3>{task.title}</h3>{task.description ? <p>{task.description}</p> : null}<small>{task.dueDate ? `Due ${new Date(task.dueDate).toLocaleDateString()}` : 'No due date'}</small></div>
      <div className="task-item__actions">
        <select aria-label={`Status for ${task.title}`} value={task.status} disabled={!canManage} onChange={(event) => void onStatusChange(Number(event.target.value) as ProjectTaskStatus)}>{Object.entries(statusLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select>
        <span className={`priority priority--${priorityLabels[task.priority].toLowerCase()}`}>{priorityLabels[task.priority]}</span>
        <button className="button button--ghost" type="button" aria-expanded={discussionOpen} onClick={onToggleDiscussion}>Discussion</button>
        {canManage ? <><button className="button button--ghost" type="button" onClick={onEdit}>Edit</button><button className="button button--danger" type="button" disabled={deleting} onClick={async () => { setDeleting(true); try { await onDelete(); } finally { setDeleting(false); } }}>Delete</button></> : null}
      </div>
    </article>
  );
}

function TaskDiscussion({ taskId, comments, loading, canComment, canDeleteComment, onCreate, onDelete }: { taskId: string; comments?: { id: string; authorUserId: string; authorDisplayName: string; content: string; createdAt: string }[]; loading: boolean; canComment: boolean; canDeleteComment: (authorUserId: string) => boolean; onCreate: (content: string) => Promise<unknown>; onDelete: (commentId: string) => Promise<void> }) {
  const [content, setContent] = useState('');
  const [saving, setSaving] = useState(false);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!content.trim()) return;
    setSaving(true);
    try {
      await onCreate(content.trim());
      setContent('');
    } finally {
      setSaving(false);
    }
  };

  return (
    <section className="task-discussion" aria-label="Task discussion">
      <h3>Discussion</h3>
      {loading ? <p className="page-note">Loading comments...</p> : comments?.length ? <div className="task-discussion__list">{comments.map((comment) => <article key={comment.id} className="task-discussion__comment"><div><strong>{comment.authorDisplayName}</strong><small>{new Date(comment.createdAt).toLocaleString()}</small></div><p>{comment.content}</p>{canDeleteComment(comment.authorUserId) ? <button className="button button--danger" type="button" onClick={() => void onDelete(comment.id)}>Delete comment</button> : null}</article>)}</div> : <p className="page-note">No comments yet.</p>}
      {canComment ? <form className="task-discussion__form" onSubmit={submit}><label className="field__label" htmlFor={`task-comment-${taskId}`}>Add a comment</label><textarea id={`task-comment-${taskId}`} value={content} onChange={(event) => setContent(event.target.value)} rows={3} maxLength={2000} required /><button className="button" type="submit" disabled={saving}>{saving ? 'Posting...' : 'Post comment'}</button></form> : <p className="page-note">Viewers can read comments but cannot add them.</p>}
    </section>
  );
}

export default function Projects() {
  const { projectArchiveEnabled, projectTaskAssignmentEnabled } = useFeatureAvailability();
  const { user } = useAuth();
  const { projects, selectedProject, tasks, loading, tasksLoading, error, members, availableMembers, activities, activitiesLoading, dashboard, dashboardLoading, taskComments, commentsLoadingTaskId, projectInvitations, invitationsLoading, includeArchived, setIncludeArchived, projectScope, setProjectScope, selectProject, createProject, updateProject, archiveProject, createTask, updateTask, updateTaskStatus, deleteTask, loadTaskComments, createTaskComment, deleteTaskComment, loadProjectInvitations, createProjectInvitation, addMember, removeMember, updateMemberRole, clearError, taskPage, taskSearch, taskTotalPages, setTaskPage, setTaskSearch } = useProjects();
  const [editing, setEditing] = useState(false);
  const [editingTaskId, setEditingTaskId] = useState<string | null>(null);
  const [statusFilter, setStatusFilter] = useState<'all' | ProjectTaskStatus>('all');
  const [priorityFilter, setPriorityFilter] = useState<'all' | ProjectTaskPriority>('all');
  const [openDiscussionTaskId, setOpenDiscussionTaskId] = useState<string | null>(null);
  const visibleTasks = useMemo(() => tasks.filter((task) => (statusFilter === 'all' || task.status === statusFilter) && (priorityFilter === 'all' || task.priority === priorityFilter)), [priorityFilter, statusFilter, tasks]);
  const isProjectOwner = selectedProject ? selectedProject.currentUserRole === ProjectMemberRole.Owner || selectedProject.ownerId === user?.id : false;

  return (
    <section className="page-shell projects-page">
      <header className="page-shell__header"><div><p className="eyebrow">Workspace</p><h1>Projects</h1><p className="page-note">Keep the work visible, ordered, and moving.</p></div></header>
      {error ? <div className="form__error" role="alert">{error}<button className="button button--ghost" type="button" onClick={clearError}>Dismiss</button></div> : null}
      {loading ? <div className="page-state"><p>Loading projects...</p></div> : (
        <div className="projects-layout">
          <aside className="projects-sidebar">
            <div className="card"><label className="field__label" htmlFor="project-scope">Project scope</label><select id="project-scope" value={projectScope ?? 'all'} onChange={(event) => void setProjectScope?.(event.target.value as 'all' | 'owned' | 'member')}><option value="all">All accessible</option><option value="owned">Owned by me</option><option value="member">I am a member</option></select></div>
            <div className="card"><h2>New project</h2><ProjectForm onSubmit={async (name, description) => { await createProject({ name, description }); }} /></div>
            <div className="card"><h2>{includeArchived ? 'All projects' : 'Your projects'}</h2><label className="toggle-field"><input type="checkbox" checked={includeArchived} onChange={(event) => void setIncludeArchived(event.target.checked)} /> Show archived</label>{projects.length === 0 ? <p className="page-note">No projects found.</p> : <div className="project-list">{projects.map((project) => <button className={`project-list__item ${selectedProject?.id === project.id ? 'project-list__item--active' : ''}`} type="button" key={project.id} onClick={() => void selectProject(project.id)}><strong>{project.name}</strong><span>{project.isArchived ? 'Archived' : project.description || 'No description'}</span></button>)}</div>}</div>
          </aside>
          <div className="projects-content">{selectedProject ? <>
            <div className="card project-heading"><div><p className="eyebrow">Selected project</p><h2>{selectedProject.name}</h2><p>{selectedProject.description || 'No description'}</p>{selectedProject.isArchived ? <span className="priority priority--high">Archived</span> : null}</div><div className="hero__actions">{isProjectOwner && !selectedProject.isArchived ? <button className="button button--ghost" type="button" onClick={() => setEditing((value) => !value)}>{editing ? 'Close editor' : 'Edit'}</button> : null}{projectArchiveEnabled && isProjectOwner && !selectedProject.isArchived ? <button className="button button--danger" type="button" onClick={() => { if (window.confirm(`Archive ${selectedProject.name}?`)) void archiveProject(selectedProject.id); }}>Archive</button> : null}</div></div>
            {editing ? <div className="card"><ProjectForm project={selectedProject} onCancel={() => setEditing(false)} onSubmit={async (name, description) => { await updateProject(selectedProject.id, { name, description }); setEditing(false); }} /></div> : null}
            <section className="dashboard" aria-labelledby="dashboard-heading">
              <div className="dashboard__header"><div><p className="eyebrow">Project dashboard</p><h2 id="dashboard-heading">Work at a glance</h2></div></div>
              {dashboardLoading ? <p className="page-note">Loading dashboard...</p> : !dashboard ? <p className="page-note">Dashboard data is unavailable.</p> : <>
                <div className="dashboard__metrics" aria-label="Task status summary">
                  <div><span>Total</span><strong>{dashboard.totalTasks}</strong></div>
                  <div><span>To do</span><strong>{dashboard.todoTasks}</strong></div>
                  <div><span>In progress</span><strong>{dashboard.inProgressTasks}</strong></div>
                  <div><span>Done</span><strong>{dashboard.doneTasks}</strong></div>
                </div>
                <div className="dashboard__content">
                  <div><h3>Priority</h3><dl className="dashboard__priority"><div><dt>High</dt><dd>{dashboard.highPriorityTasks}</dd></div><div><dt>Normal</dt><dd>{dashboard.normalPriorityTasks}</dd></div><div><dt>Low</dt><dd>{dashboard.lowPriorityTasks}</dd></div></dl></div>
                  <div><h3>Past due</h3>{dashboard.overdueTasks.length === 0 ? <p className="page-note">Nothing overdue.</p> : <ul className="dashboard__task-list">{dashboard.overdueTasks.map((task) => <li key={task.id}><strong>{task.title}</strong><small>Due {new Date(task.dueDate!).toLocaleDateString()}</small></li>)}</ul>}</div>
                  <div><h3>Next 7 days</h3>{dashboard.upcomingTasks.length === 0 ? <p className="page-note">Nothing due soon.</p> : <ul className="dashboard__task-list">{dashboard.upcomingTasks.map((task) => <li key={task.id}><strong>{task.title}</strong><small>Due {new Date(task.dueDate!).toLocaleDateString()}</small></li>)}</ul>}</div>
                  <div><h3>Latest activity</h3>{dashboard.recentActivities.length === 0 ? <p className="page-note">No activity yet.</p> : <ul className="dashboard__activity-list">{dashboard.recentActivities.map((activity) => <li key={activity.id}><strong>{activity.actorDisplayName}</strong><span>{activity.description}</span></li>)}</ul>}</div>
                </div>
              </>}
            </section>
            {!selectedProject.isArchived && projectTaskAssignmentEnabled && isProjectOwner ? <MemberPanel members={members} availableMembers={availableMembers} ownerId={selectedProject.ownerId} onAdd={addMember} onRemove={removeMember} onRoleChange={updateMemberRole} /> : null}
            {!selectedProject.isArchived && isProjectOwner ? <InvitationPanel projectId={selectedProject.id} invitations={projectInvitations} loading={invitationsLoading} onLoad={loadProjectInvitations} onCreate={createProjectInvitation} /> : null}
            {!selectedProject.isArchived && selectedProject.currentUserRole !== ProjectMemberRole.Viewer ? <div className="card"><h2>Add task</h2><TaskForm members={members} assignmentEnabled={projectTaskAssignmentEnabled} onSubmit={createTask} /></div> : null}
            <div className="card"><label className="field__label" htmlFor="task-search">Search tasks</label><input id="task-search" value={taskSearch ?? ''} onChange={(event) => { setTaskSearch?.(event.target.value); setTaskPage?.(1); }} /><div className="hero__actions"><button className="button button--ghost" type="button" disabled={!taskPage || taskPage <= 1} onClick={() => setTaskPage?.((taskPage ?? 1) - 1)}>Previous</button><span className="role-badge">Page {taskPage ?? 1} of {taskTotalPages ?? 0}</span><button className="button button--ghost" type="button" disabled={!taskTotalPages || (taskPage ?? 1) >= taskTotalPages} onClick={() => setTaskPage?.((taskPage ?? 1) + 1)}>Next</button></div></div>
            <div className="card">
              <div className="page-shell__header">
                <div><h2>Tasks</h2><p className="page-note">Filter the current project without another request.</p></div>
                <span className="role-badge">{visibleTasks.length} of {tasks.length}</span>
              </div>
              <div className="toolbar">
                <div className="toolbar__group">
                  <label>Status<select className="toolbar__select" value={statusFilter} onChange={(event) => setStatusFilter(event.target.value === 'all' ? 'all' : Number(event.target.value) as ProjectTaskStatus)}><option value="all">All statuses</option>{Object.entries(statusLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
                  <label>Priority<select className="toolbar__select" value={priorityFilter} onChange={(event) => setPriorityFilter(event.target.value === 'all' ? 'all' : Number(event.target.value) as ProjectTaskPriority)}><option value="all">All priorities</option>{Object.entries(priorityLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
                </div>
              </div>
              {tasksLoading ? <p className="page-note">Loading tasks...</p> : visibleTasks.length === 0 ? <p className="page-note">No tasks match the current filters.</p> : (
                <div className="task-list">
                  {visibleTasks.map((task) => {
                    const canManage = isProjectOwner || (selectedProject.currentUserRole === ProjectMemberRole.Member && task.createdByUserId === user?.id);
                    if (editingTaskId === task.id) return <div className="card" key={task.id}><TaskForm initialTask={task} members={members} assignmentEnabled={projectTaskAssignmentEnabled} onSubmit={async (request) => { await updateTask(task.id, request); setEditingTaskId(null); }} /><button className="button button--ghost" type="button" onClick={() => setEditingTaskId(null)}>Cancel</button></div>;
                    const discussionOpen = openDiscussionTaskId === task.id;
                    return <div key={task.id}><TaskItem task={task} canManage={canManage} discussionOpen={discussionOpen} onToggleDiscussion={() => { const opening = !discussionOpen; setOpenDiscussionTaskId(opening ? task.id : null); if (opening && !taskComments[task.id]) void loadTaskComments(task.id); }} onEdit={() => setEditingTaskId(task.id)} onStatusChange={(status) => updateTaskStatus(task.id, status).then(() => undefined)} onDelete={() => deleteTask(task.id)} />{discussionOpen ? <TaskDiscussion taskId={task.id} comments={taskComments[task.id]} loading={commentsLoadingTaskId === task.id} canComment={!selectedProject.isArchived && selectedProject.currentUserRole !== ProjectMemberRole.Viewer} canDeleteComment={(authorUserId) => isProjectOwner || authorUserId === user?.id} onCreate={(content) => createTaskComment(task.id, content)} onDelete={(commentId) => deleteTaskComment(task.id, commentId)} /> : null}</div>;
                  })}
                </div>
              )}
            </div>
            <div className="card">
              <div className="page-shell__header">
                <div><h2>Activity</h2><p className="page-note">Recent changes made by project members.</p></div>
              </div>
              {activitiesLoading ? <p className="page-note">Loading activity...</p> : activities.length === 0 ? <p className="page-note">No activity recorded yet.</p> : (
                <div className="member-list">
                  {activities.map((activity) => (
                    <div className="member-list__item" key={activity.id}>
                      <span><strong>{activity.actorDisplayName}</strong><small>{activity.description}</small></span>
                      <small>{new Date(activity.createdAt).toLocaleString()}</small>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </> : <div className="page-state"><h2>Select a project</h2><p>Create your first project or choose one from the list.</p></div>}</div>
        </div>
      )}
    </section>
  );
}