import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { projectApi } from '../services/api/ProjectApi';
import type {
  CreateProjectRequest,
  CreateProjectTaskRequest,
  ProjectDto,
  ProjectTaskDto,
  ProjectTaskStatus,
  ProjectMemberDto,
  ProjectMemberUserDto,
  ProjectMemberRole,
  ProjectActivityDto,
  ProjectDashboardDto,
  ProjectTaskQuery,
  ProjectTaskCommentDto,
  ProjectTaskAttachmentDto,
  ProjectInvitationDto,
  CreatedProjectInvitationDto,
  CreateProjectInvitationRequest,
  UpdateProjectRequest,
  UpdateProjectTaskRequest,
} from '../types';

interface ProjectsContextValue {
  projects: ProjectDto[];
  selectedProject: ProjectDto | null;
  tasks: ProjectTaskDto[];
  loading: boolean;
  tasksLoading: boolean;
  error: string | null;
  members: ProjectMemberDto[];
  availableMembers: ProjectMemberUserDto[];
  activities: ProjectActivityDto[];
  activitiesLoading: boolean;
  dashboard: ProjectDashboardDto | null;
  dashboardLoading: boolean;
  taskComments: Record<string, ProjectTaskCommentDto[]>;
  commentsLoadingTaskId: string | null;
  taskAttachments: Record<string, ProjectTaskAttachmentDto[]>;
  attachmentsLoadingTaskId: string | null;
  projectInvitations: ProjectInvitationDto[];
  invitationsLoading: boolean;
  includeArchived: boolean;
  setIncludeArchived: (includeArchived: boolean) => Promise<void>;
  projectScope?: 'all' | 'owned' | 'member';
  setProjectScope?: (scope: 'all' | 'owned' | 'member') => Promise<void>;
  refreshProjects: () => Promise<void>;
  selectProject: (projectId: string) => Promise<void>;
  createProject: (request: CreateProjectRequest) => Promise<ProjectDto>;
  updateProject: (projectId: string, request: UpdateProjectRequest) => Promise<ProjectDto>;
  archiveProject: (projectId: string) => Promise<void>;
  createTask: (request: CreateProjectTaskRequest) => Promise<ProjectTaskDto>;
  updateTask: (taskId: string, request: UpdateProjectTaskRequest) => Promise<ProjectTaskDto>;
  updateTaskStatus: (taskId: string, status: ProjectTaskStatus) => Promise<ProjectTaskDto>;
  deleteTask: (taskId: string) => Promise<void>;
  loadTaskComments: (taskId: string) => Promise<void>;
  createTaskComment: (taskId: string, content: string) => Promise<ProjectTaskCommentDto>;
  deleteTaskComment: (taskId: string, commentId: string) => Promise<void>;
  loadTaskAttachments: (taskId: string) => Promise<void>;
  uploadTaskAttachment: (taskId: string, file: File) => Promise<ProjectTaskAttachmentDto>;
  downloadTaskAttachment: (taskId: string, attachmentId: string) => Promise<Blob>;
  deleteTaskAttachment: (taskId: string, attachmentId: string) => Promise<void>;
  loadProjectInvitations: (projectId: string) => Promise<void>;
  createProjectInvitation: (request: CreateProjectInvitationRequest) => Promise<CreatedProjectInvitationDto>;
  acceptProjectInvitation: (token: string) => Promise<void>;
  declineProjectInvitation: (token: string) => Promise<void>;
  addMember: (userId: string) => Promise<void>;
  removeMember: (userId: string) => Promise<void>;
  updateMemberRole?: (userId: string, role: ProjectMemberRole) => Promise<void>;
  clearError: () => void;
  taskPage?: number;
  taskSearch?: string;
  taskTotalPages?: number;
  setTaskPage?: (page: number) => void;
  setTaskSearch?: (search: string) => void;
  taskFilters?: Omit<ProjectTaskQuery, 'pageNumber' | 'pageSize' | 'search'>;
  setTaskFilters?: (filters: Partial<Omit<ProjectTaskQuery, 'pageNumber' | 'pageSize' | 'search'>>) => void;
}

const ProjectsContext = createContext<ProjectsContextValue | undefined>(undefined);

export function ProjectsProvider({ children }: { children: ReactNode }) {
  const [projects, setProjects] = useState<ProjectDto[]>([]);
  const [selectedProjectId, setSelectedProjectId] = useState<string | null>(null);
  const [tasks, setTasks] = useState<ProjectTaskDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [tasksLoading, setTasksLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [members, setMembers] = useState<ProjectMemberDto[]>([]);
  const [availableMembers, setAvailableMembers] = useState<ProjectMemberUserDto[]>([]);
  const [activities, setActivities] = useState<ProjectActivityDto[]>([]);
  const [activitiesLoading, setActivitiesLoading] = useState(false);
  const [dashboard, setDashboard] = useState<ProjectDashboardDto | null>(null);
  const [dashboardLoading, setDashboardLoading] = useState(false);
  const [taskComments, setTaskComments] = useState<Record<string, ProjectTaskCommentDto[]>>({});
  const [commentsLoadingTaskId, setCommentsLoadingTaskId] = useState<string | null>(null);
  const [taskAttachments, setTaskAttachments] = useState<Record<string, ProjectTaskAttachmentDto[]>>({});
  const [attachmentsLoadingTaskId, setAttachmentsLoadingTaskId] = useState<string | null>(null);
  const [projectInvitations, setProjectInvitations] = useState<ProjectInvitationDto[]>([]);
  const [invitationsLoading, setInvitationsLoading] = useState(false);
  const [includeArchived, setIncludeArchivedState] = useState(false);
  const [projectScope, setProjectScopeState] = useState<'all' | 'owned' | 'member'>('all');
  const [taskPage, setTaskPage] = useState(1);
  const [taskSearch, setTaskSearch] = useState('');
  const [taskFilters, setTaskFiltersState] = useState<Omit<ProjectTaskQuery, 'pageNumber' | 'pageSize' | 'search'>>({});
  const [taskTotalPages, setTaskTotalPages] = useState(0);

  const loadProjects = useCallback(async (loadArchived = includeArchived, scope = projectScope) => {
    setLoading(true);
    setError(null);

    try {
      const response = await projectApi.getProjects(loadArchived, scope);
      const nextProjects = response.data ?? [];
      setProjects(nextProjects);
      setSelectedProjectId((currentId) =>
        currentId && nextProjects.some((project) => project.id === currentId)
          ? currentId
          : nextProjects[0]?.id ?? null,
      );
    } catch (caughtError) {
      setError(caughtError instanceof Error ? caughtError.message : 'Unable to load projects');
    } finally {
      setLoading(false);
    }
  }, [includeArchived, projectScope]);

  const loadTasks = useCallback(async (projectId: string) => {
    setTasksLoading(true);
    setError(null);

    try {
      const response = await projectApi.getTasks(projectId, {
        pageNumber: taskPage,
        pageSize: 20,
        search: taskSearch,
        ...taskFilters,
      });
      setTasks(response.data?.items ?? []);
      setTaskTotalPages(response.data?.totalPages ?? 0);
    } catch (caughtError) {
      setTasks([]);
      setError(caughtError instanceof Error ? caughtError.message : 'Unable to load tasks');
    } finally {
      setTasksLoading(false);
    }
  }, [taskFilters, taskPage, taskSearch]);

  const setTaskFilters = useCallback((filters: Partial<Omit<ProjectTaskQuery, 'pageNumber' | 'pageSize' | 'search'>>) => {
    setTaskFiltersState((currentFilters) => ({ ...currentFilters, ...filters }));
    setTaskPage(1);
  }, []);

  const loadMembers = useCallback(async (projectId: string) => {
    try {
      const [membersResponse, availableResponse] = await Promise.all([
        projectApi.getMembers(projectId),
        projectApi.getAvailableMembers(projectId),
      ]);
      setMembers(membersResponse.data ?? []);
      setAvailableMembers(availableResponse.data ?? []);
    } catch (caughtError) {
      setMembers([]);
      setAvailableMembers([]);
      setError(caughtError instanceof Error ? caughtError.message : 'Unable to load project members');
    }
  }, []);

  const loadActivities = useCallback(async (projectId: string) => {
    setActivitiesLoading(true);
    try {
      const response = await projectApi.getActivity(projectId);
      setActivities(response.data?.items ?? []);
    } catch (caughtError) {
      setActivities([]);
      setError(caughtError instanceof Error ? caughtError.message : 'Unable to load project activity');
    } finally {
      setActivitiesLoading(false);
    }
  }, []);

  const loadDashboard = useCallback(async (projectId: string) => {
    setDashboardLoading(true);
    try {
      const response = await projectApi.getDashboard(projectId);
      setDashboard(response.data ?? null);
    } catch (caughtError) {
      setDashboard(null);
      setError(caughtError instanceof Error ? caughtError.message : 'Unable to load project dashboard');
    } finally {
      setDashboardLoading(false);
    }
  }, []);

  const loadTaskComments = useCallback(async (taskId: string) => {
    if (!selectedProjectId) return;

    setCommentsLoadingTaskId(taskId);
    try {
      const response = await projectApi.getTaskComments(selectedProjectId, taskId);
      setTaskComments((currentComments) => ({ ...currentComments, [taskId]: response.data ?? [] }));
    } catch (caughtError) {
      setError(caughtError instanceof Error ? caughtError.message : 'Unable to load task comments');
    } finally {
      setCommentsLoadingTaskId((currentTaskId) => currentTaskId === taskId ? null : currentTaskId);
    }
  }, [selectedProjectId]);

  const loadProjectInvitations = useCallback(async (projectId: string) => {
    setInvitationsLoading(true);
    try {
      const response = await projectApi.getInvitations(projectId);
      setProjectInvitations(response.data ?? []);
    } catch (caughtError) {
      setProjectInvitations([]);
      setError(caughtError instanceof Error ? caughtError.message : 'Unable to load project invitations');
    } finally {
      setInvitationsLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadProjects();
  }, [loadProjects]);

  useEffect(() => {
    if (selectedProjectId) {
      void loadTasks(selectedProjectId);
      void loadMembers(selectedProjectId);
      void loadActivities(selectedProjectId);
      void loadDashboard(selectedProjectId);
    } else {
      setTasks([]);
      setMembers([]);
      setAvailableMembers([]);
      setActivities([]);
      setDashboard(null);
      setTaskComments({});
      setTaskAttachments({});
      setProjectInvitations([]);
    }
  }, [loadActivities, loadDashboard, loadMembers, loadTasks, selectedProjectId]);

  const selectedProject = projects.find((project) => project.id === selectedProjectId) ?? null;

  const selectProject = useCallback(async (projectId: string) => {
    setSelectedProjectId(projectId);
    setTaskPage(1);
  }, []);

  const setIncludeArchived = useCallback(async (nextIncludeArchived: boolean) => {
    setIncludeArchivedState(nextIncludeArchived);
    await loadProjects(nextIncludeArchived);
  }, [loadProjects]);

  const setProjectScope = useCallback(async (nextScope: 'all' | 'owned' | 'member') => {
    setProjectScopeState(nextScope);
    await loadProjects(includeArchived, nextScope);
  }, [includeArchived, loadProjects]);

  const createProject = useCallback(async (request: CreateProjectRequest) => {
    setError(null);
    const response = await projectApi.createProject(request);
    if (!response.data) {
      throw new Error(response.message || 'Project was not created');
    }

    setProjects((currentProjects) => [response.data!, ...currentProjects]);
    setSelectedProjectId(response.data.id);
    return response.data;
  }, []);

  const updateProject = useCallback(async (projectId: string, request: UpdateProjectRequest) => {
    setError(null);
    const response = await projectApi.updateProject(projectId, request);
    if (!response.data) {
      throw new Error(response.message || 'Project was not updated');
    }

    setProjects((currentProjects) =>
      currentProjects.map((project) => (project.id === projectId ? response.data! : project)),
    );
    return response.data;
  }, []);

  const archiveProject = useCallback(async (projectId: string) => {
    setError(null);
    await projectApi.archiveProject(projectId);
    setProjects((currentProjects) => currentProjects.filter((project) => project.id !== projectId));
    setSelectedProjectId((currentId) => (currentId === projectId ? null : currentId));
    setTasks([]);
    setTaskComments({});
    setProjectInvitations([]);
  }, []);

  const createTask = useCallback(async (request: CreateProjectTaskRequest) => {
    if (!selectedProjectId) {
      throw new Error('Select a project first');
    }

    setError(null);
    const response = await projectApi.createTask(selectedProjectId, request);
    if (!response.data) {
      throw new Error(response.message || 'Task was not created');
    }

    setTasks((currentTasks) => [...currentTasks, response.data!]);
    void loadActivities(selectedProjectId);
    void loadDashboard(selectedProjectId);
    return response.data;
  }, [loadActivities, loadDashboard, selectedProjectId]);

  const updateTask = useCallback(async (taskId: string, request: UpdateProjectTaskRequest) => {
    if (!selectedProjectId) {
      throw new Error('Select a project first');
    }

    setError(null);
    const response = await projectApi.updateTask(selectedProjectId, taskId, request);
    if (!response.data) {
      throw new Error(response.message || 'Task was not updated');
    }

    setTasks((currentTasks) => currentTasks.map((task) => (task.id === taskId ? response.data! : task)));
    void loadActivities(selectedProjectId);
    void loadDashboard(selectedProjectId);
    return response.data;
  }, [loadActivities, loadDashboard, selectedProjectId]);

  const updateTaskStatus = useCallback(async (taskId: string, status: ProjectTaskStatus) => {
    if (!selectedProjectId) {
      throw new Error('Select a project first');
    }

    setError(null);
    const response = await projectApi.updateTaskStatus(selectedProjectId, taskId, { status });
    if (!response.data) {
      throw new Error(response.message || 'Task status was not updated');
    }

    setTasks((currentTasks) => currentTasks.map((task) => (task.id === taskId ? response.data! : task)));
    void loadActivities(selectedProjectId);
    void loadDashboard(selectedProjectId);
    return response.data;
  }, [loadActivities, loadDashboard, selectedProjectId]);

  const deleteTask = useCallback(async (taskId: string) => {
    if (!selectedProjectId) {
      throw new Error('Select a project first');
    }

    setError(null);
    await projectApi.deleteTask(selectedProjectId, taskId);
    setTasks((currentTasks) => currentTasks.filter((task) => task.id !== taskId));
    setTaskComments((currentComments) => {
      const { [taskId]: _, ...remainingComments } = currentComments;
      return remainingComments;
    });
    setTaskAttachments((currentAttachments) => {
      const { [taskId]: _, ...remainingAttachments } = currentAttachments;
      return remainingAttachments;
    });
    void loadActivities(selectedProjectId);
    void loadDashboard(selectedProjectId);
  }, [loadActivities, loadDashboard, selectedProjectId]);

  const createTaskComment = useCallback(async (taskId: string, content: string) => {
    if (!selectedProjectId) {
      throw new Error('Select a project first');
    }

    setError(null);
    const response = await projectApi.createTaskComment(selectedProjectId, taskId, { content });
    if (!response.data) {
      throw new Error(response.message || 'Task comment was not created');
    }

    setTaskComments((currentComments) => ({
      ...currentComments,
      [taskId]: [...(currentComments[taskId] ?? []), response.data!],
    }));
    void loadActivities(selectedProjectId);
    void loadDashboard(selectedProjectId);
    return response.data;
  }, [loadActivities, loadDashboard, selectedProjectId]);

  const deleteTaskComment = useCallback(async (taskId: string, commentId: string) => {
    if (!selectedProjectId) {
      throw new Error('Select a project first');
    }

    setError(null);
    await projectApi.deleteTaskComment(selectedProjectId, taskId, commentId);
    setTaskComments((currentComments) => ({
      ...currentComments,
      [taskId]: (currentComments[taskId] ?? []).filter((comment) => comment.id !== commentId),
    }));
  }, [selectedProjectId]);

  const loadTaskAttachments = useCallback(async (taskId: string) => {
    if (!selectedProjectId) return;

    setAttachmentsLoadingTaskId(taskId);
    try {
      const response = await projectApi.getTaskAttachments(selectedProjectId, taskId);
      setTaskAttachments((currentAttachments) => ({ ...currentAttachments, [taskId]: response.data ?? [] }));
    } catch (caughtError) {
      setError(caughtError instanceof Error ? caughtError.message : 'Unable to load task attachments');
    } finally {
      setAttachmentsLoadingTaskId((currentTaskId) => currentTaskId === taskId ? null : currentTaskId);
    }
  }, [selectedProjectId]);

  const uploadTaskAttachment = useCallback(async (taskId: string, file: File) => {
    if (!selectedProjectId) {
      throw new Error('Select a project first');
    }

    setError(null);
    const response = await projectApi.uploadTaskAttachment(selectedProjectId, taskId, file);
    if (!response.data) {
      throw new Error(response.message || 'Task attachment was not uploaded');
    }

    setTaskAttachments((currentAttachments) => ({
      ...currentAttachments,
      [taskId]: [response.data!, ...(currentAttachments[taskId] ?? [])],
    }));
    void loadActivities(selectedProjectId);
    void loadDashboard(selectedProjectId);
    return response.data;
  }, [loadActivities, loadDashboard, selectedProjectId]);

  const downloadTaskAttachment = useCallback(async (taskId: string, attachmentId: string) => {
    if (!selectedProjectId) {
      throw new Error('Select a project first');
    }

    return projectApi.downloadTaskAttachment(selectedProjectId, taskId, attachmentId);
  }, [selectedProjectId]);

  const deleteTaskAttachment = useCallback(async (taskId: string, attachmentId: string) => {
    if (!selectedProjectId) {
      throw new Error('Select a project first');
    }

    setError(null);
    await projectApi.deleteTaskAttachment(selectedProjectId, taskId, attachmentId);
    setTaskAttachments((currentAttachments) => ({
      ...currentAttachments,
      [taskId]: (currentAttachments[taskId] ?? []).filter((attachment) => attachment.id !== attachmentId),
    }));
    void loadActivities(selectedProjectId);
    void loadDashboard(selectedProjectId);
  }, [loadActivities, loadDashboard, selectedProjectId]);

  const createProjectInvitation = useCallback(async (request: CreateProjectInvitationRequest) => {
    if (!selectedProjectId) {
      throw new Error('Select a project first');
    }

    setError(null);
    const response = await projectApi.createInvitation(selectedProjectId, request);
    if (!response.data) {
      throw new Error(response.message || 'Project invitation was not created');
    }

    setProjectInvitations((currentInvitations) => [response.data!.invitation, ...currentInvitations]);
    void loadActivities(selectedProjectId);
    void loadDashboard(selectedProjectId);
    return response.data;
  }, [loadActivities, loadDashboard, selectedProjectId]);

  const acceptProjectInvitation = useCallback(async (token: string) => {
    setError(null);
    const response = await projectApi.acceptInvitation(token);
    if (!response.data) {
      throw new Error(response.message || 'Project invitation was not accepted');
    }

    await loadProjects();
  }, [loadProjects]);

  const declineProjectInvitation = useCallback(async (token: string) => {
    setError(null);
    const response = await projectApi.declineInvitation(token);
    if (!response.data) {
      throw new Error(response.message || 'Project invitation was not declined');
    }
  }, []);

  const addMember = useCallback(async (userId: string) => {
    if (!selectedProjectId) {
      throw new Error('Select a project first');
    }

    const response = await projectApi.addMember(selectedProjectId, userId);
    if (!response.data) {
      throw new Error(response.message || 'Member was not added');
    }

    setMembers((currentMembers) => [...currentMembers, response.data!]);
    setAvailableMembers((currentUsers) => currentUsers.filter((user) => user.id !== userId));
    void loadActivities(selectedProjectId);
  }, [loadActivities, selectedProjectId]);

  const removeMember = useCallback(async (userId: string) => {
    if (!selectedProjectId) {
      throw new Error('Select a project first');
    }

    await projectApi.removeMember(selectedProjectId, userId);
    const removedMember = members.find((member) => member.userId === userId);
    setMembers((currentMembers) => currentMembers.filter((member) => member.userId !== userId));
    if (removedMember) {
      setAvailableMembers((currentUsers) => [...currentUsers, {
        id: removedMember.userId,
        displayName: removedMember.displayName,
        email: removedMember.email,
      }]);
    }
    void loadActivities(selectedProjectId);
  }, [loadActivities, members, selectedProjectId]);

  const updateMemberRole = useCallback(async (userId: string, role: ProjectMemberRole) => {
    if (!selectedProjectId) {
      throw new Error('Select a project first');
    }

    const response = await projectApi.updateMemberRole(selectedProjectId, userId, role);
    if (!response.data) {
      throw new Error(response.message || 'Member role was not updated');
    }

    setMembers((currentMembers) => currentMembers.map((member) => (
      member.userId === userId ? response.data! : member
    )));
  }, [selectedProjectId]);

  const value = useMemo<ProjectsContextValue>(() => ({
    projects,
    selectedProject,
    tasks,
    loading,
    tasksLoading,
    error,
    members,
    availableMembers,
    activities,
    activitiesLoading,
    dashboard,
    dashboardLoading,
    taskComments,
    commentsLoadingTaskId,
    taskAttachments,
    attachmentsLoadingTaskId,
    projectInvitations,
    invitationsLoading,
    includeArchived,
    setIncludeArchived,
    projectScope,
    setProjectScope,
    refreshProjects: () => loadProjects(),
    selectProject,
    createProject,
    updateProject,
    archiveProject,
    createTask,
    updateTask,
    updateTaskStatus,
    deleteTask,
    loadTaskComments,
    createTaskComment,
    deleteTaskComment,
    loadTaskAttachments,
    uploadTaskAttachment,
    downloadTaskAttachment,
    deleteTaskAttachment,
    loadProjectInvitations,
    createProjectInvitation,
    acceptProjectInvitation,
    declineProjectInvitation,
    addMember,
    removeMember,
    updateMemberRole,
    clearError: () => setError(null),
    taskPage,
    taskSearch,
    taskTotalPages,
    setTaskPage,
    setTaskSearch,
    taskFilters,
    setTaskFilters,
  }), [
    projects,
    selectedProject,
    tasks,
    loading,
    tasksLoading,
    error,
    members,
    availableMembers,
    activities,
    activitiesLoading,
    dashboard,
    dashboardLoading,
    taskComments,
    commentsLoadingTaskId,
    taskAttachments,
    attachmentsLoadingTaskId,
    projectInvitations,
    invitationsLoading,
    includeArchived,
    projectScope,
    setIncludeArchived,
    setProjectScope,
    loadProjects,
    selectProject,
    createProject,
    updateProject,
    archiveProject,
    createTask,
    updateTask,
    updateTaskStatus,
    deleteTask,
    loadTaskComments,
    createTaskComment,
    deleteTaskComment,
    loadTaskAttachments,
    uploadTaskAttachment,
    downloadTaskAttachment,
    deleteTaskAttachment,
    loadProjectInvitations,
    createProjectInvitation,
    acceptProjectInvitation,
    declineProjectInvitation,
    addMember,
    removeMember,
    updateMemberRole,
    taskPage,
    taskSearch,
    taskTotalPages,
    taskFilters,
    setTaskFilters,
  ]);

  return <ProjectsContext.Provider value={value}>{children}</ProjectsContext.Provider>;
}

export function useProjects() {
  const context = useContext(ProjectsContext);
  if (!context) {
    throw new Error('useProjects must be used within ProjectsProvider');
  }

  return context;
}