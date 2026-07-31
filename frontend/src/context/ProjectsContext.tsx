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
  addMember: (userId: string) => Promise<void>;
  removeMember: (userId: string) => Promise<void>;
  updateMemberRole?: (userId: string, role: ProjectMemberRole) => Promise<void>;
  clearError: () => void;
  taskPage?: number;
  taskSearch?: string;
  taskTotalPages?: number;
  setTaskPage?: (page: number) => void;
  setTaskSearch?: (search: string) => void;
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
  const [includeArchived, setIncludeArchivedState] = useState(false);
  const [projectScope, setProjectScopeState] = useState<'all' | 'owned' | 'member'>('all');
  const [taskPage, setTaskPage] = useState(1);
  const [taskSearch, setTaskSearch] = useState('');
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
      const response = await projectApi.getTasks(projectId, taskPage, 20, taskSearch);
      setTasks(response.data?.items ?? []);
      setTaskTotalPages(response.data?.totalPages ?? 0);
    } catch (caughtError) {
      setTasks([]);
      setError(caughtError instanceof Error ? caughtError.message : 'Unable to load tasks');
    } finally {
      setTasksLoading(false);
    }
  }, [taskPage, taskSearch]);

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

  useEffect(() => {
    void loadProjects();
  }, [loadProjects]);

  useEffect(() => {
    if (selectedProjectId) {
      void loadTasks(selectedProjectId);
      void loadMembers(selectedProjectId);
      void loadActivities(selectedProjectId);
    } else {
      setTasks([]);
      setMembers([]);
      setAvailableMembers([]);
      setActivities([]);
    }
  }, [loadActivities, loadMembers, loadTasks, selectedProjectId]);

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
    return response.data;
  }, [loadActivities, selectedProjectId]);

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
    return response.data;
  }, [loadActivities, selectedProjectId]);

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
    return response.data;
  }, [loadActivities, selectedProjectId]);

  const deleteTask = useCallback(async (taskId: string) => {
    if (!selectedProjectId) {
      throw new Error('Select a project first');
    }

    setError(null);
    await projectApi.deleteTask(selectedProjectId, taskId);
    setTasks((currentTasks) => currentTasks.filter((task) => task.id !== taskId));
  }, [selectedProjectId]);

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
    addMember,
    removeMember,
    updateMemberRole,
    clearError: () => setError(null),
    taskPage,
    taskSearch,
    taskTotalPages,
    setTaskPage,
    setTaskSearch,
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
    addMember,
    removeMember,
    updateMemberRole,
    taskPage,
    taskSearch,
    taskTotalPages,
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