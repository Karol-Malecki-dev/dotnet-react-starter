import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import Projects from '../../pages/Projects';
import { useProjects } from '../../context/ProjectsContext';
import { useAuth } from '../../hooks/useAuth';
import { useFeatureAvailability } from '../../hooks/useFeatureAvailability';
import { ProjectMemberRole, ProjectTaskPriority, ProjectTaskStatus } from '../../types';
import { projectApi } from '../../services/api';

import { vi } from 'vitest';

vi.mock('../../context/ProjectsContext');
vi.mock('../../hooks/useAuth');
vi.mock('../../hooks/useFeatureAvailability');

const mockedUseProjects = useProjects as jest.MockedFunction<typeof useProjects>;
const mockedUseAuth = useAuth as jest.MockedFunction<typeof useAuth>;
const mockedUseFeatureAvailability = useFeatureAvailability as jest.MockedFunction<typeof useFeatureAvailability>;

const project = {
  id: 'project-1',
  name: 'Website refresh',
  description: 'A focused delivery project',
  ownerId: 'owner-1',
  createdAt: '2026-07-25T10:00:00Z',
  updatedAt: '2026-07-25T10:00:00Z',
  isArchived: false,
};

const task = {
  id: 'task-1',
  projectId: 'project-1',
  title: 'Prepare wireframes',
  description: 'First design pass',
  status: ProjectTaskStatus.Todo,
  priority: ProjectTaskPriority.High,
  dueDate: null,
  assignedUserId: null,
  createdAt: '2026-07-25T10:00:00Z',
  updatedAt: '2026-07-25T10:00:00Z',
  labels: [],
};

const ownerMember = {
  userId: 'owner-1',
  displayName: 'Owner',
  email: 'owner@example.com',
  addedAt: '2026-07-25T10:00:00Z',
};

const additionalMember = {
  userId: 'member-1',
  displayName: 'Member',
  email: 'member@example.com',
  addedAt: '2026-07-25T10:00:00Z',
};

const availableUser = {
  id: 'available-1',
  displayName: 'Available User',
  email: 'available@example.com',
};

function createContextValue(overrides = {}) {
  return {
    projects: [project],
    selectedProject: project,
    tasks: [task],
    loading: false,
    tasksLoading: false,
    error: null,
    members: [ownerMember],
    availableMembers: [],
    activities: [],
    activitiesLoading: false,
    dashboard: null,
    dashboardLoading: false,
    taskComments: {},
    commentsLoadingTaskId: null,
    taskAttachments: {},
    attachmentsLoadingTaskId: null,
    projectInvitations: [],
    invitationsLoading: false,
    includeArchived: false,
    setIncludeArchived: jest.fn().mockResolvedValue(undefined),
    refreshProjects: jest.fn().mockResolvedValue(undefined),
    selectProject: jest.fn().mockResolvedValue(undefined),
    createProject: jest.fn().mockResolvedValue(project),
    updateProject: jest.fn().mockResolvedValue(project),
    archiveProject: jest.fn().mockResolvedValue(undefined),
    createTask: jest.fn().mockResolvedValue(task),
    updateTask: jest.fn().mockResolvedValue(task),
    updateTaskStatus: jest.fn().mockResolvedValue(task),
    deleteTask: jest.fn().mockResolvedValue(undefined),
    loadTaskComments: jest.fn().mockResolvedValue(undefined),
    createTaskComment: jest.fn().mockResolvedValue(undefined),
    deleteTaskComment: jest.fn().mockResolvedValue(undefined),
    loadTaskAttachments: jest.fn().mockResolvedValue(undefined),
    uploadTaskAttachment: jest.fn().mockResolvedValue(undefined),
    downloadTaskAttachment: jest.fn().mockResolvedValue(new Blob()),
    deleteTaskAttachment: jest.fn().mockResolvedValue(undefined),
    loadProjectInvitations: jest.fn().mockResolvedValue(undefined),
    createProjectInvitation: jest.fn().mockResolvedValue(undefined),
    acceptProjectInvitation: jest.fn().mockResolvedValue(undefined),
    declineProjectInvitation: jest.fn().mockResolvedValue(undefined),
    addMember: jest.fn().mockResolvedValue(undefined),
    removeMember: jest.fn().mockResolvedValue(undefined),
    clearError: jest.fn(),
    ...overrides,
  } as ReturnType<typeof useProjects>;
}

describe('Projects page', () => {
  beforeEach(() => {
    jest.resetAllMocks();
    mockedUseAuth.mockReturnValue({ user: { id: 'owner-1', displayName: 'Owner', role: 'User' } } as any);
    mockedUseFeatureAvailability.mockReturnValue({
      loading: false,
      loaded: true,
      error: null,
      globalSearchEnabled: true,
      dashboardOverviewEnabled: true,
      adminNavigationEnabled: false,
      userManagementNavigationEnabled: false,
      emailFeatureSectionsEnabled: false,
      emailDeliveryEnabled: false,
      emailTwoFactorEnabled: false,
      emailTwoFactorEnabledForNewUsers: false,
      projectsEnabled: true,
      projectArchiveEnabled: true,
      projectTaskAssignmentEnabled: true,
    });
    mockedUseProjects.mockReturnValue(createContextValue());
  });

  it('renders the selected project and its task', () => {
    render(<Projects />);

    expect(screen.getByRole('heading', { name: 'Projects' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Website refresh' })).toBeInTheDocument();
    expect(screen.getByText('Prepare wireframes')).toBeInTheDocument();
    expect(screen.getByText('High', { selector: 'span.priority' })).toBeInTheDocument();
  });

  it('scrolls to a task requested through the project navigation URL', () => {
    const scrollIntoView = vi.fn();
    Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', { configurable: true, value: scrollIntoView });
    window.history.replaceState({}, '', '/projects?projectId=project-1&taskId=task-1');

    render(<Projects />);

    expect(scrollIntoView).toHaveBeenCalledWith({ behavior: 'smooth', block: 'center' });
    window.history.replaceState({}, '', '/');
  });

  it('loads a requested task outside the current page before scrolling to it', async () => {
    const scrollIntoView = vi.fn();
    Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', { configurable: true, value: scrollIntoView });
    const requestedTask = { ...task, id: 'task-2', title: 'Off-page task' };
    vi.spyOn(projectApi, 'getTask').mockResolvedValue({
      data: requestedTask,
      message: 'Task loaded',
      statusCode: 200,
      errors: [],
      timestamp: '2026-07-31T10:00:00Z',
    });
    window.history.replaceState({}, '', '/projects?projectId=project-1&taskId=task-2');
    mockedUseProjects.mockReturnValue(createContextValue({ tasks: [] }));

    render(<Projects />);

    await waitFor(() => expect(screen.getByText('Off-page task')).toBeInTheDocument());
    expect(projectApi.getTask).toHaveBeenCalledWith('project-1', 'task-2');
    expect(scrollIntoView).toHaveBeenCalledWith({ behavior: 'smooth', block: 'center' });
    window.history.replaceState({}, '', '/');
  });

  it('renders recent project activity', () => {
    mockedUseProjects.mockReturnValue(createContextValue({
      activities: [{
        id: 'activity-1',
        type: 'task.created',
        description: "created the task 'Prepare wireframes'.",
        actorUserId: 'owner-1',
        actorDisplayName: 'Owner',
        projectTaskId: 'task-1',
        createdAt: '2026-07-25T10:00:00Z',
      }],
    }));

    render(<Projects />);

    expect(screen.getByRole('heading', { name: 'Activity' })).toBeInTheDocument();
    expect(screen.getByText("created the task 'Prepare wireframes'.")).toBeInTheDocument();
  });

  it('renders project dashboard task metrics and deadlines', () => {
    mockedUseProjects.mockReturnValue(createContextValue({
      dashboard: {
        totalTasks: 3,
        todoTasks: 1,
        inProgressTasks: 1,
        doneTasks: 1,
        lowPriorityTasks: 1,
        normalPriorityTasks: 1,
        highPriorityTasks: 1,
        overdueTasks: [{ ...task, id: 'overdue-task', title: 'Fix checkout', dueDate: '2026-07-20T00:00:00Z' }],
        upcomingTasks: [{ ...task, id: 'upcoming-task', title: 'Review copy', dueDate: '2026-07-28T00:00:00Z' }],
        recentActivities: [],
      },
    }));

    render(<Projects />);

    expect(screen.getByRole('heading', { name: 'Work at a glance' })).toBeInTheDocument();
    expect(screen.getByText('Fix checkout')).toBeInTheDocument();
    expect(screen.getByText('Review copy')).toBeInTheDocument();
  });

  it('opens task discussion and submits a comment through the projects context', async () => {
    const loadTaskComments = jest.fn().mockResolvedValue(undefined);
    const createTaskComment = jest.fn().mockResolvedValue(undefined);
    mockedUseProjects.mockReturnValue(createContextValue({ loadTaskComments, createTaskComment }));
    render(<Projects />);

    fireEvent.click(screen.getByRole('button', { name: 'Discussion' }));
    expect(loadTaskComments).toHaveBeenCalledWith(task.id);
    fireEvent.change(screen.getByLabelText('Add a comment'), { target: { value: 'I will review this.' } });
    fireEvent.click(screen.getByRole('button', { name: 'Post comment' }));

    await waitFor(() => expect(createTaskComment).toHaveBeenCalledWith(task.id, 'I will review this.'));
  });

  it('lazy-loads task attachments and uploads the selected file', async () => {
    const loadTaskAttachments = jest.fn().mockResolvedValue(undefined);
    const uploadTaskAttachment = jest.fn().mockResolvedValue(undefined);
    mockedUseProjects.mockReturnValue(createContextValue({ loadTaskAttachments, uploadTaskAttachment }));
    render(<Projects />);

    fireEvent.click(screen.getByRole('button', { name: 'Attachments' }));
    expect(loadTaskAttachments).toHaveBeenCalledWith(task.id);

    const file = new File(['plain text'], 'notes.txt', { type: 'text/plain' });
    fireEvent.change(screen.getByLabelText('Choose a file'), { target: { files: [file] } });
    fireEvent.click(screen.getByRole('button', { name: 'Upload attachment' }));

    await waitFor(() => expect(uploadTaskAttachment).toHaveBeenCalledWith(task.id, file));
  });

  it('lets viewers download attachments but hides upload and delete controls', () => {
    mockedUseAuth.mockReturnValue({ user: { id: 'viewer-1', displayName: 'Viewer', role: 'User' } } as any);
    mockedUseProjects.mockReturnValue(createContextValue({
      selectedProject: { ...project, currentUserRole: ProjectMemberRole.Viewer },
      taskAttachments: {
        [task.id]: [{
          id: 'attachment-1',
          projectTaskId: task.id,
          uploadedByUserId: 'owner-1',
          uploaderDisplayName: 'Owner',
          originalFileName: 'release-notes.txt',
          contentType: 'text/plain',
          sizeBytes: 12,
          createdAt: '2026-07-25T10:00:00Z',
        }],
      },
    }));
    render(<Projects />);

    fireEvent.click(screen.getByRole('button', { name: 'Attachments' }));

    expect(screen.getByText('release-notes.txt')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Download' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Upload attachment' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Delete' })).not.toBeInTheDocument();
  });

  it('creates a project invitation through the projects context', async () => {
    const loadProjectInvitations = jest.fn().mockResolvedValue(undefined);
    const createProjectInvitation = jest.fn().mockResolvedValue({ token: 'invitation-token' });
    mockedUseProjects.mockReturnValue(createContextValue({ loadProjectInvitations, createProjectInvitation }));
    render(<Projects />);

    expect(loadProjectInvitations).toHaveBeenCalledWith(project.id);
    fireEvent.change(screen.getByLabelText('Account email'), { target: { value: 'new.member@example.com' } });
    fireEvent.click(screen.getByRole('button', { name: 'Create invitation' }));

    await waitFor(() => expect(createProjectInvitation).toHaveBeenCalledWith({
      email: 'new.member@example.com',
      role: ProjectMemberRole.Member,
    }));
  });

  it('submits a new task through the projects context', async () => {
    const createTask = jest.fn().mockResolvedValue(task);
    mockedUseProjects.mockReturnValue(createContextValue({ createTask }));
    render(<Projects />);

    fireEvent.change(screen.getByLabelText('Task title'), { target: { value: 'Review copy' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add task' }));

    await waitFor(() => expect(createTask).toHaveBeenCalledWith({
      title: 'Review copy',
      description: undefined,
      priority: ProjectTaskPriority.Normal,
      dueDate: undefined,
      assignedUserId: undefined,
    }));
  });

  it('renders task labels and submits normalized labels from the task form', async () => {
    const createTask = jest.fn().mockResolvedValue(task);
    mockedUseProjects.mockReturnValue(createContextValue({
      tasks: [{ ...task, labels: ['design', 'urgent'] }],
      createTask,
    }));
    render(<Projects />);

    expect(screen.getByText('design')).toBeInTheDocument();
    expect(screen.getByText('urgent')).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('Task title'), { target: { value: 'Review labels' } });
    fireEvent.change(screen.getByLabelText('Labels'), { target: { value: ' frontend, urgent ' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add task' }));

    await waitFor(() => expect(createTask).toHaveBeenCalledWith(expect.objectContaining({
      title: 'Review labels',
      labels: ['frontend', 'urgent'],
    })));
  });

  it('changes task status through the projects context', async () => {
    const updateTaskStatus = jest.fn().mockResolvedValue(task);
    mockedUseProjects.mockReturnValue(createContextValue({ updateTaskStatus }));
    render(<Projects />);

    fireEvent.change(screen.getByRole('combobox', { name: 'Status for Prepare wireframes' }), {
      target: { value: String(ProjectTaskStatus.Done) },
    });

    await waitFor(() => expect(updateTaskStatus).toHaveBeenCalledWith('task-1', ProjectTaskStatus.Done));
  });

  it('shows task management controls for the project owner', () => {
    render(<Projects />);

    expect(within(screen.getByRole('article')).getByRole('button', { name: 'Edit' })).toBeInTheDocument();
    expect(within(screen.getByRole('article')).getByRole('button', { name: 'Delete' })).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Status for Prepare wireframes' })).toBeEnabled();
  });

  it('shows task management controls for a member on their own task', () => {
    const memberTask = { ...task, createdByUserId: 'member-1' };
    mockedUseAuth.mockReturnValue({ user: { id: 'member-1', displayName: 'Member', role: 'User' } } as any);
    mockedUseProjects.mockReturnValue(createContextValue({
      selectedProject: { ...project, currentUserRole: ProjectMemberRole.Member },
      tasks: [memberTask],
    }));
    render(<Projects />);

    expect(within(screen.getByRole('article')).getByRole('button', { name: 'Edit' })).toBeInTheDocument();
    expect(within(screen.getByRole('article')).getByRole('button', { name: 'Delete' })).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Status for Prepare wireframes' })).toBeEnabled();
  });

  it('hides task management controls for a viewer', () => {
    mockedUseAuth.mockReturnValue({ user: { id: 'viewer-1', displayName: 'Viewer', role: 'User' } } as any);
    mockedUseProjects.mockReturnValue(createContextValue({
      selectedProject: { ...project, currentUserRole: ProjectMemberRole.Viewer },
    }));
    render(<Projects />);

    expect(within(screen.getByRole('article')).queryByRole('button', { name: 'Edit' })).not.toBeInTheDocument();
    expect(within(screen.getByRole('article')).queryByRole('button', { name: 'Delete' })).not.toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Status for Prepare wireframes' })).toBeDisabled();
    expect(screen.queryByRole('heading', { name: 'Add task' })).not.toBeInTheDocument();
  });

  it('updates the server task query when task filters change', () => {
    const setTaskFilters = jest.fn();
    mockedUseProjects.mockReturnValue(createContextValue({ setTaskFilters }));
    render(<Projects />);

    fireEvent.change(screen.getAllByLabelText('Status')[0], { target: { value: String(ProjectTaskStatus.Done) } });
    const priorityFilters = screen.getAllByLabelText('Priority');
    fireEvent.change(priorityFilters[priorityFilters.length - 1], { target: { value: String(ProjectTaskPriority.Low) } });

    expect(setTaskFilters).toHaveBeenCalledWith({ status: ProjectTaskStatus.Done });
    expect(setTaskFilters).toHaveBeenCalledWith({ priority: ProjectTaskPriority.Low });
  });

  it('submits edited task data with the selected project member', async () => {
    const updateTask = jest.fn().mockResolvedValue(task);
    mockedUseProjects.mockReturnValue(createContextValue({ updateTask }));
    render(<Projects />);

    fireEvent.click(within(screen.getByRole('article')).getByRole('button', { name: 'Edit' }));
    fireEvent.change(screen.getByDisplayValue('Prepare wireframes'), { target: { value: 'Updated wireframes' } });
    const assigneeSelects = document.querySelectorAll<HTMLSelectElement>('#task-assignee');
    fireEvent.change(assigneeSelects[assigneeSelects.length - 1], { target: { value: ownerMember.userId } });
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(updateTask).toHaveBeenCalledWith('task-1', expect.objectContaining({
      title: 'Updated wireframes',
      priority: ProjectTaskPriority.High,
      assignedUserId: ownerMember.userId,
    })));
  });

  it('loads archived projects when the archive toggle changes', async () => {
    const setIncludeArchived = jest.fn().mockResolvedValue(undefined);
    mockedUseProjects.mockReturnValue(createContextValue({ setIncludeArchived }));
    render(<Projects />);

    fireEvent.click(screen.getByLabelText('Show archived'));

    await waitFor(() => expect(setIncludeArchived).toHaveBeenCalledWith(true));
  });

  it('adds an available user to the project', async () => {
    const addMember = jest.fn().mockResolvedValue(undefined);
    mockedUseProjects.mockReturnValue(createContextValue({ addMember, availableMembers: [availableUser] }));
    render(<Projects />);

    fireEvent.change(screen.getByLabelText('Available project users'), {
      target: { value: availableUser.id },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Add member' }));

    await waitFor(() => expect(addMember).toHaveBeenCalledWith(availableUser.id));
  });

  it('removes a non-owner project member', async () => {
    const removeMember = jest.fn().mockResolvedValue(undefined);
    mockedUseProjects.mockReturnValue(createContextValue({ members: [ownerMember, additionalMember], removeMember }));
    render(<Projects />);

    fireEvent.click(screen.getByRole('button', { name: 'Remove' }));

    await waitFor(() => expect(removeMember).toHaveBeenCalledWith(additionalMember.userId));
  });

  it('hides member management for archived projects', () => {
    mockedUseProjects.mockReturnValue(createContextValue({ selectedProject: { ...project, isArchived: true } }));
    render(<Projects />);

    expect(screen.queryByRole('heading', { name: 'Project members' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Add member' })).not.toBeInTheDocument();
  });
});