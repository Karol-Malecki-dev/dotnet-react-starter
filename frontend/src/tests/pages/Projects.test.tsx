import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import Projects from '../../pages/Projects';
import { useProjects } from '../../context/ProjectsContext';
import { useAuth } from '../../hooks/useAuth';
import { useFeatureAvailability } from '../../hooks/useFeatureAvailability';
import { ProjectMemberRole, ProjectTaskPriority, ProjectTaskStatus } from '../../types';

jest.mock('../../context/ProjectsContext');
jest.mock('../../hooks/useAuth');
jest.mock('../../hooks/useFeatureAvailability');

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

  it('filters tasks by status and priority', () => {
    const doneTask = { ...task, id: 'task-2', title: 'Ship release', status: ProjectTaskStatus.Done, priority: ProjectTaskPriority.Low };
    mockedUseProjects.mockReturnValue(createContextValue({ tasks: [task, doneTask] }));
    render(<Projects />);

    fireEvent.change(screen.getAllByLabelText('Status')[0], { target: { value: String(ProjectTaskStatus.Done) } });
    fireEvent.change(screen.getAllByLabelText('Priority')[0], { target: { value: String(ProjectTaskPriority.Low) } });

    expect(screen.queryByText('Prepare wireframes')).not.toBeInTheDocument();
    expect(screen.getByText('Ship release')).toBeInTheDocument();
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