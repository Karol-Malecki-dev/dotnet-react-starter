import { useEffect, useMemo, useRef, useState } from 'react';
import {
  ProjectTaskPriority,
  ProjectTaskSortBy,
  ProjectTaskStatus,
  SortDirection,
  ProjectMemberRole,
  ProjectInvitationStatus,
  type CreateProjectTaskRequest,
  type ProjectDto,
  type ProjectMemberDto,
  type ProjectTaskDto,
  type ProjectTaskAttachmentDto,
} from '../types';
import { useProjects } from '../context/ProjectsContext';
import { useFeatureAvailability } from '../hooks/useFeatureAvailability';
import { useAuth } from '../hooks/useAuth';
import { projectApi } from '../services/api';

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
  const [labels, setLabels] = useState(initialTask?.labels.join(', ') ?? '');
  const [saving, setSaving] = useState(false);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!title.trim()) return;
    setSaving(true);
    try {
      const normalizedLabels = labels.split(',').map((label) => label.trim()).filter(Boolean);
      await onSubmit({ title: title.trim(), description: description.trim() || undefined, priority, dueDate: dueDate || undefined, assignedUserId: assignedUserId || undefined, ...(normalizedLabels.length ? { labels: normalizedLabels } : {}) });
      if (!initialTask) {
        setTitle('');
        setDescription('');
        setDueDate('');
        setAssignedUserId('');
        setLabels('');
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
      <div className="field"><label className="field__label" htmlFor="task-labels">Labels</label><input id="task-labels" value={labels} onChange={(event) => setLabels(event.target.value)} placeholder="frontend, urgent" /></div>
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

function TaskItem({ task, canManage, discussionOpen, attachmentsOpen, onToggleDiscussion, onToggleAttachments, onStatusChange, onDelete, onEdit }: { task: ProjectTaskDto; canManage: boolean; discussionOpen: boolean; attachmentsOpen: boolean; onToggleDiscussion: () => void; onToggleAttachments: () => void; onStatusChange: (status: ProjectTaskStatus) => Promise<void>; onDelete: () => Promise<void>; onEdit: () => void }) {
  const [deleting, setDeleting] = useState(false);
  return (
    <article className="task-item" id={`project-task-${task.id}`}>
      <div><h3>{task.title}</h3>{task.description ? <p>{task.description}</p> : null}<div className="task-labels">{task.labels.map((label) => <span className="task-label" key={label}>{label}</span>)}</div><small>{task.dueDate ? `Due ${new Date(task.dueDate).toLocaleDateString()}` : 'No due date'}</small></div>
      <div className="task-item__actions">
        <select aria-label={`Status for ${task.title}`} value={task.status} disabled={!canManage} onChange={(event) => void onStatusChange(Number(event.target.value) as ProjectTaskStatus)}>{Object.entries(statusLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select>
        <span className={`priority priority--${priorityLabels[task.priority].toLowerCase()}`}>{priorityLabels[task.priority]}</span>
        <button className="button button--ghost" type="button" aria-expanded={discussionOpen} onClick={onToggleDiscussion}>Discussion</button>
        <button className="button button--ghost" type="button" aria-expanded={attachmentsOpen} onClick={onToggleAttachments}>Attachments</button>
        {canManage ? <><button className="button button--ghost" type="button" onClick={onEdit}>Edit</button><button className="button button--danger" type="button" disabled={deleting} onClick={async () => { setDeleting(true); try { await onDelete(); } finally { setDeleting(false); } }}>Delete</button></> : null}
      </div>
    </article>
  );
}

function TaskBoard({ tasks, canManageTask, onStatusChange }: { tasks: ProjectTaskDto[]; canManageTask: (task: ProjectTaskDto) => boolean; onStatusChange: (taskId: string, status: ProjectTaskStatus) => Promise<void> }) {
  const columns = [ProjectTaskStatus.Todo, ProjectTaskStatus.InProgress, ProjectTaskStatus.Done];

  return (
    <div className="task-board" role="region" aria-label="Task board">
      {columns.map((status) => {
        const columnTasks = tasks.filter((task) => task.status === status);
        return (
          <section className="task-board__column" key={status} aria-labelledby={`task-board-${status}`}>
            <header className="task-board__column-header"><h3 id={`task-board-${status}`}>{statusLabels[status]}</h3><span className="role-badge">{columnTasks.length}</span></header>
            <div className="task-board__cards">
              {columnTasks.length === 0 ? <p className="page-note">No tasks.</p> : columnTasks.map((task) => (
                <article className="task-board__card" id={`project-task-${task.id}`} key={task.id}>
                  <div><h4>{task.title}</h4>{task.description ? <p>{task.description}</p> : null}</div>
                  <div className="task-labels">{task.labels.map((label) => <span className="task-label" key={label}>{label}</span>)}</div>
                  <div className="task-board__meta"><span className={`priority priority--${priorityLabels[task.priority].toLowerCase()}`}>{priorityLabels[task.priority]}</span><small>{task.dueDate ? `Due ${new Date(task.dueDate).toLocaleDateString()}` : 'No due date'}</small></div>
                  <label className="field field--inline"><span className="field__label">Move {task.title} to</span><select value={task.status} disabled={!canManageTask(task)} onChange={(event) => void onStatusChange(task.id, Number(event.target.value) as ProjectTaskStatus)}>{Object.entries(statusLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
                </article>
              ))}
            </div>
          </section>
        );
      })}
    </div>
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

function formatAttachmentSize(sizeBytes: number) {
  if (sizeBytes < 1024) return `${sizeBytes} B`;
  if (sizeBytes < 1024 * 1024) return `${(sizeBytes / 1024).toFixed(1)} KB`;
  return `${(sizeBytes / (1024 * 1024)).toFixed(1)} MB`;
}

function TaskAttachments({ taskId, attachments, loading, canUpload, canDelete, onUpload, onDownload, onDelete }: { taskId: string; attachments?: ProjectTaskAttachmentDto[]; loading: boolean; canUpload: boolean; canDelete: (uploadedByUserId: string) => boolean; onUpload: (file: File) => Promise<unknown>; onDownload: (attachmentId: string) => Promise<Blob>; onDelete: (attachmentId: string) => Promise<void> }) {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [saving, setSaving] = useState(false);
  const [downloadingId, setDownloadingId] = useState<string | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [validationError, setValidationError] = useState<string | null>(null);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!selectedFile) return;
    if (selectedFile.size > 10 * 1024 * 1024) {
      setValidationError('Attachments must be 10 MB or smaller.');
      return;
    }

    setSaving(true);
    setValidationError(null);
    try {
      await onUpload(selectedFile);
      setSelectedFile(null);
      if (fileInputRef.current) fileInputRef.current.value = '';
    } finally {
      setSaving(false);
    }
  };

  const download = async (attachment: ProjectTaskAttachmentDto) => {
    setDownloadingId(attachment.id);
    try {
      const blob = await onDownload(attachment.id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = attachment.originalFileName;
      link.click();
      URL.revokeObjectURL(url);
    } finally {
      setDownloadingId(null);
    }
  };

  const remove = async (attachmentId: string) => {
    setDeletingId(attachmentId);
    try {
      await onDelete(attachmentId);
    } finally {
      setDeletingId(null);
    }
  };

  return (
    <section className="task-attachments" aria-label="Task attachments">
      <h3>Attachments</h3>
      {loading ? <p className="page-note">Loading attachments...</p> : attachments?.length ? <div className="task-attachments__list">{attachments.map((attachment) => <article className="task-attachments__item" key={attachment.id}><div><strong>{attachment.originalFileName}</strong><small>{attachment.uploaderDisplayName} · {new Date(attachment.createdAt).toLocaleString()} · {formatAttachmentSize(attachment.sizeBytes)}</small></div><div className="task-attachments__actions"><button className="button button--ghost" type="button" disabled={downloadingId === attachment.id} onClick={() => void download(attachment)}>{downloadingId === attachment.id ? 'Downloading...' : 'Download'}</button>{canDelete(attachment.uploadedByUserId) ? <button className="button button--danger" type="button" disabled={deletingId === attachment.id} onClick={() => void remove(attachment.id)}>{deletingId === attachment.id ? 'Deleting...' : 'Delete'}</button> : null}</div></article>)}</div> : <p className="page-note">No attachments yet.</p>}
      {canUpload ? <form className="task-attachments__form" onSubmit={submit}><label className="field__label" htmlFor={`task-attachment-${taskId}`}>Choose a file</label><input ref={fileInputRef} id={`task-attachment-${taskId}`} type="file" accept=".pdf,.png,.jpg,.jpeg,.docx,.xlsx,.txt" onChange={(event) => { setSelectedFile(event.target.files?.[0] ?? null); setValidationError(null); }} /><button className="button" type="submit" disabled={!selectedFile || saving}>{saving ? 'Uploading...' : 'Upload attachment'}</button>{validationError ? <p className="field__error" role="alert">{validationError}</p> : null}</form> : <p className="page-note">Viewers can download attachments but cannot upload them.</p>}
    </section>
  );
}

export default function Projects() {
  const { projectArchiveEnabled, projectTaskAssignmentEnabled } = useFeatureAvailability();
  const { user } = useAuth();
  const { projects, selectedProject, tasks, loading, tasksLoading, error, members, availableMembers, activities, activitiesLoading, dashboard, dashboardLoading, taskComments, commentsLoadingTaskId, taskAttachments, attachmentsLoadingTaskId, projectInvitations, invitationsLoading, includeArchived, setIncludeArchived, projectScope, setProjectScope, selectProject, createProject, updateProject, archiveProject, createTask, updateTask, updateTaskStatus, deleteTask, loadTaskComments, createTaskComment, deleteTaskComment, loadTaskAttachments, uploadTaskAttachment, downloadTaskAttachment, deleteTaskAttachment, loadProjectInvitations, createProjectInvitation, addMember, removeMember, updateMemberRole, clearError, taskPage, taskSearch, taskTotalPages, setTaskPage, setTaskSearch, taskFilters, setTaskFilters } = useProjects();
  const [editing, setEditing] = useState(false);
  const [editingTaskId, setEditingTaskId] = useState<string | null>(null);
  const [taskView, setTaskView] = useState<'list' | 'board'>('list');
  const [openDiscussionTaskId, setOpenDiscussionTaskId] = useState<string | null>(null);
  const [openAttachmentsTaskId, setOpenAttachmentsTaskId] = useState<string | null>(null);
  const [requestedTask, setRequestedTask] = useState<ProjectTaskDto | null>(null);
  const isProjectOwner = selectedProject ? selectedProject.currentUserRole === ProjectMemberRole.Owner || selectedProject.ownerId === user?.id : false;
  const requestedProjectId = new URLSearchParams(window.location.search).get('projectId');
  const requestedTaskId = new URLSearchParams(window.location.search).get('taskId');

  useEffect(() => {
    if (requestedProjectId
      && selectedProject?.id !== requestedProjectId
      && projects.some((project) => project.id === requestedProjectId)) {
      void selectProject(requestedProjectId);
    }
  }, [projects, requestedProjectId, selectProject, selectedProject?.id]);

  useEffect(() => {
    if (!requestedTaskId || selectedProject?.id !== requestedProjectId || tasksLoading) return;

    document.getElementById(`project-task-${requestedTaskId}`)?.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }, [requestedProjectId, requestedTaskId, requestedTask, selectedProject?.id, tasks, tasksLoading]);

  useEffect(() => {
    if (!requestedTaskId || selectedProject?.id !== requestedProjectId || tasks.some((task) => task.id === requestedTaskId)) {
      setRequestedTask(null);
      return;
    }

    let cancelled = false;
    void projectApi.getTask(selectedProject.id, requestedTaskId).then((response) => {
      if (!cancelled) setRequestedTask(response.data ?? null);
    }).catch(() => {
      if (!cancelled) setRequestedTask(null);
    });
    return () => { cancelled = true; };
  }, [requestedProjectId, requestedTaskId, selectedProject?.id, tasks]);

  const displayedTasks = requestedTask && !tasks.some((task) => task.id === requestedTask.id)
    ? [...tasks, requestedTask]
    : tasks;
  const visibleTasks = displayedTasks;

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
            {editing ? <div className="card"><ProjectForm project={selectedProject} onCancel={() => setEditing(false)} onSubmit={async (name, description) => { await updateProject(selectedProject.id, { name, description, concurrencyStamp: selectedProject.concurrencyStamp }); setEditing(false); }} /></div> : null}
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
                <div><h2>Tasks</h2><p className="page-note">Filter and sort tasks in this project.</p></div>
                <div className="hero__actions"><span className="role-badge">{visibleTasks.length} of {tasks.length}</span><div className="segmented-control" aria-label="Task view"><button className={`segmented-control__button ${taskView === 'list' ? 'segmented-control__button--active' : ''}`} type="button" aria-pressed={taskView === 'list'} onClick={() => setTaskView('list')}>List</button><button className={`segmented-control__button ${taskView === 'board' ? 'segmented-control__button--active' : ''}`} type="button" aria-pressed={taskView === 'board'} onClick={() => setTaskView('board')}>Board</button></div></div>
              </div>
              <div className="toolbar">
                <div className="toolbar__group">
                  <label>Status<select className="toolbar__select" value={taskFilters?.status ?? 'all'} onChange={(event) => setTaskFilters?.({ status: event.target.value === 'all' ? undefined : Number(event.target.value) as ProjectTaskStatus })}><option value="all">All statuses</option>{Object.entries(statusLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
                  <label>Priority<select className="toolbar__select" value={taskFilters?.priority ?? 'all'} onChange={(event) => setTaskFilters?.({ priority: event.target.value === 'all' ? undefined : Number(event.target.value) as ProjectTaskPriority })}><option value="all">All priorities</option>{Object.entries(priorityLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
                  <label>Assignee<select className="toolbar__select" value={taskFilters?.assignedUserId ?? ''} onChange={(event) => setTaskFilters?.({ assignedUserId: event.target.value || undefined })}><option value="">All assignees</option>{members.map((member) => <option key={member.userId} value={member.userId}>{member.displayName}</option>)}</select></label>
                  <label>Label<input className="toolbar__select" value={taskFilters?.label ?? ''} onChange={(event) => setTaskFilters?.({ label: event.target.value || undefined })} /></label>
                  <label>Due before<input className="toolbar__select" type="date" value={taskFilters?.dueBefore ?? ''} onChange={(event) => setTaskFilters?.({ dueBefore: event.target.value || undefined })} /></label>
                  <label>Sort by<select className="toolbar__select" value={taskFilters?.sortBy ?? ProjectTaskSortBy.DueDate} onChange={(event) => setTaskFilters?.({ sortBy: event.target.value as ProjectTaskSortBy })}><option value={ProjectTaskSortBy.DueDate}>Due date</option><option value={ProjectTaskSortBy.CreatedAt}>Created date</option><option value={ProjectTaskSortBy.Priority}>Priority</option></select></label>
                  <label>Order<select className="toolbar__select" value={taskFilters?.sortDirection ?? SortDirection.Ascending} onChange={(event) => setTaskFilters?.({ sortDirection: event.target.value as SortDirection })}><option value={SortDirection.Ascending}>Ascending</option><option value={SortDirection.Descending}>Descending</option></select></label>
                </div>
              </div>
              {tasksLoading ? <p className="page-note">Loading tasks...</p> : visibleTasks.length === 0 ? <p className="page-note">No tasks match the current filters.</p> : taskView === 'board' ? <TaskBoard tasks={visibleTasks} canManageTask={(task) => isProjectOwner || (selectedProject.currentUserRole === ProjectMemberRole.Member && task.createdByUserId === user?.id)} onStatusChange={(taskId, status) => updateTaskStatus(taskId, status).then(() => undefined)} /> : (
                <div className="task-list">
                  {visibleTasks.map((task) => {
                    const canManage = isProjectOwner || (selectedProject.currentUserRole === ProjectMemberRole.Member && task.createdByUserId === user?.id);
                    if (editingTaskId === task.id) return <div className="card" key={task.id}><TaskForm initialTask={task} members={members} assignmentEnabled={projectTaskAssignmentEnabled} onSubmit={async (request) => { await updateTask(task.id, request); setEditingTaskId(null); }} /><button className="button button--ghost" type="button" onClick={() => setEditingTaskId(null)}>Cancel</button></div>;
                    const discussionOpen = openDiscussionTaskId === task.id;
                    const attachmentsOpen = openAttachmentsTaskId === task.id;
                    return <div key={task.id}><TaskItem task={task} canManage={canManage} discussionOpen={discussionOpen} attachmentsOpen={attachmentsOpen} onToggleDiscussion={() => { const opening = !discussionOpen; setOpenDiscussionTaskId(opening ? task.id : null); if (opening && !taskComments[task.id]) void loadTaskComments(task.id); }} onToggleAttachments={() => { const opening = !attachmentsOpen; setOpenAttachmentsTaskId(opening ? task.id : null); if (opening && !taskAttachments[task.id]) void loadTaskAttachments(task.id); }} onEdit={() => setEditingTaskId(task.id)} onStatusChange={(status) => updateTaskStatus(task.id, status).then(() => undefined)} onDelete={() => deleteTask(task.id)} />{discussionOpen ? <TaskDiscussion taskId={task.id} comments={taskComments[task.id]} loading={commentsLoadingTaskId === task.id} canComment={!selectedProject.isArchived && selectedProject.currentUserRole !== ProjectMemberRole.Viewer} canDeleteComment={(authorUserId) => isProjectOwner || authorUserId === user?.id} onCreate={(content) => createTaskComment(task.id, content)} onDelete={(commentId) => deleteTaskComment(task.id, commentId)} /> : null}{attachmentsOpen ? <TaskAttachments taskId={task.id} attachments={taskAttachments[task.id]} loading={attachmentsLoadingTaskId === task.id} canUpload={!selectedProject.isArchived && selectedProject.currentUserRole !== ProjectMemberRole.Viewer} canDelete={(uploadedByUserId) => isProjectOwner || uploadedByUserId === user?.id} onUpload={(file) => uploadTaskAttachment(task.id, file)} onDownload={(attachmentId) => downloadTaskAttachment(task.id, attachmentId)} onDelete={(attachmentId) => deleteTaskAttachment(task.id, attachmentId)} /> : null}</div>;
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