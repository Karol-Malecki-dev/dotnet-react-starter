import { useEffect, useState } from 'react';
import {
  ProjectTaskPriority,
  ProjectTaskSortBy,
  ProjectTaskStatus,
  SortDirection,
  ProjectMemberRole,
  type ProjectTaskDto,
} from '../types';
import { useProjects } from '../context/ProjectsContext';
import { useFeatureAvailability } from '../../../hooks/useFeatureAvailability';
import { useAuth } from '../../../hooks/useAuth';
import { projectApi } from '../api/ProjectApi';
import { statusLabels, priorityLabels } from '../components/projectLabels';
import { InvitationPanel } from '../components/InvitationPanel';
import { MemberPanel } from '../components/MemberPanel';
import { ProjectForm } from '../components/ProjectForm';
import { TaskAttachments } from '../components/TaskAttachments';
import { TaskBoard } from '../components/TaskBoard';
import { TaskDiscussion } from '../components/TaskDiscussion';
import { TaskForm } from '../components/TaskForm';
import { TaskItem } from '../components/TaskItem';
export default function Projects() {
  const { projectArchiveEnabled, projectTaskAssignmentEnabled } = useFeatureAvailability();
  const { user } = useAuth();
  const { projects, selectedProject, tasks, loading, tasksLoading, tasksError, error, members, availableMembers, activities, activitiesLoading, dashboard, dashboardLoading, taskComments, commentsLoadingTaskId, taskAttachments, attachmentsLoadingTaskId, projectInvitations, invitationsLoading, includeArchived, setIncludeArchived, projectScope, setProjectScope, selectProject, createProject, updateProject, archiveProject, createTask, updateTask, updateTaskStatus, deleteTask, loadTaskComments, createTaskComment, deleteTaskComment, loadTaskAttachments, uploadTaskAttachment, downloadTaskAttachment, deleteTaskAttachment, loadProjectInvitations, createProjectInvitation, addMember, removeMember, updateMemberRole, clearError, retryTasks, taskPage, taskSearch, taskTotalPages, setTaskPage, setTaskSearch, taskFilters, setTaskFilters } = useProjects();
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
              {tasksError ? <div className="form__error" role="alert"><span>{tasksError}</span><button className="button button--ghost" type="button" onClick={() => void retryTasks()} disabled={tasksLoading}>{tasksLoading ? 'Retrying...' : 'Retry tasks'}</button></div> : null}
              {tasksLoading ? <p className="page-note">Loading tasks...</p> : visibleTasks.length === 0 ? <p className="page-note">No tasks match the current filters.</p> : taskView === 'board' ? <TaskBoard tasks={visibleTasks} canManageTask={(task) => isProjectOwner || (selectedProject.currentUserRole === ProjectMemberRole.Member && task.createdByUserId === user?.id)} onStatusChange={(taskId, status) => updateTaskStatus(taskId, status).then(() => undefined)} /> : (
                <div className="task-list">
                  {visibleTasks.map((task) => {
                    const canManage = isProjectOwner || (selectedProject.currentUserRole === ProjectMemberRole.Member && task.createdByUserId === user?.id);
                    if (editingTaskId === task.id) return <div className="card" key={task.id}><TaskForm initialTask={task} members={members} assignmentEnabled={projectTaskAssignmentEnabled} onSubmit={async (request) => { await updateTask(task.id, { ...request, concurrencyStamp: task.concurrencyStamp }); setEditingTaskId(null); }} /><button className="button button--ghost" type="button" onClick={() => setEditingTaskId(null)}>Cancel</button></div>;
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
