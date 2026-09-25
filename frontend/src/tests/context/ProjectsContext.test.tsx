import { act, render, screen, waitFor } from '@testing-library/react';
import { vi } from 'vitest';
import { ProjectsProvider, useProjects } from '../../context/ProjectsContext';
import { projectApi } from '../../services/api/ProjectApi';
import { ProjectTaskPriority, ProjectTaskStatus } from '../../types';

vi.mock('../../services/api/ProjectApi', () => ({
  projectApi: {
    getProjects: vi.fn(),
    getTasks: vi.fn(),
    getMembers: vi.fn(),
    getAvailableMembers: vi.fn(),
    getActivity: vi.fn(),
    getDashboard: vi.fn(),
  },
}));

const mockedProjectApi = vi.mocked(projectApi);

const project = {
  id: 'project-1',
  name: 'Website refresh',
  description: null,
  ownerId: 'owner-1',
  createdAt: '2026-09-25T10:00:00Z',
  updatedAt: '2026-09-25T10:00:00Z',
  isArchived: false,
};

function createDeferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((promiseResolve, promiseReject) => {
    resolve = promiseResolve;
    reject = promiseReject;
  });

  return { promise, resolve, reject };
}

function createTasksResponse(title: string) {
  return {
    statusCode: 200,
    message: 'OK',
    data: {
      items: [{
        id: title.toLowerCase().replaceAll(' ', '-'),
        projectId: project.id,
        title,
        description: null,
        status: ProjectTaskStatus.Todo,
        priority: ProjectTaskPriority.Normal,
        dueDate: null,
        assignedUserId: null,
        createdAt: '2026-09-25T10:00:00Z',
        updatedAt: '2026-09-25T10:00:00Z',
        concurrencyStamp: `${title}-stamp`,
        labels: [],
      }],
      pageNumber: 1,
      pageSize: 20,
      totalCount: 1,
      totalPages: 1,
    },
    errors: null,
    timestamp: '2026-09-25T10:00:00Z',
  };
}

function ContextConsumer() {
  const context = useProjects();

  return (
    <>
      <div data-testid="task">{context.tasks[0]?.title ?? 'none'}</div>
      <div data-testid="tasks-loading">{context.tasksLoading ? 'loading' : 'idle'}</div>
      <button type="button" onClick={() => context.setTaskSearch?.('fresh query')}>
        Search
      </button>
    </>
  );
}

describe('ProjectsContext task request cancellation', () => {
  beforeEach(() => {
    vi.resetAllMocks();
    mockedProjectApi.getProjects.mockResolvedValue({
      statusCode: 200,
      message: 'OK',
      data: [project],
      errors: null,
      timestamp: '2026-09-25T10:00:00Z',
    });
    mockedProjectApi.getMembers.mockResolvedValue({
      statusCode: 200,
      message: 'OK',
      data: [],
      errors: null,
      timestamp: '2026-09-25T10:00:00Z',
    });
    mockedProjectApi.getAvailableMembers.mockResolvedValue({
      statusCode: 200,
      message: 'OK',
      data: [],
      errors: null,
      timestamp: '2026-09-25T10:00:00Z',
    });
    mockedProjectApi.getActivity.mockResolvedValue({
      statusCode: 200,
      message: 'OK',
      data: { items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0 },
      errors: null,
      timestamp: '2026-09-25T10:00:00Z',
    });
    mockedProjectApi.getDashboard.mockResolvedValue({
      statusCode: 200,
      message: 'OK',
      data: null,
      errors: null,
      timestamp: '2026-09-25T10:00:00Z',
    });
  });

  it('aborts the previous task request and ignores its late response', async () => {
    const firstRequest = createDeferred<ReturnType<typeof createTasksResponse>>();
    const secondRequest = createDeferred<ReturnType<typeof createTasksResponse>>();
    const signals: AbortSignal[] = [];

    mockedProjectApi.getTasks.mockImplementation((_projectId, _request, signal) => {
      if (signal) {
        signals.push(signal);
      }

      return signals.length === 1 ? firstRequest.promise : secondRequest.promise;
    });

    render(
      <ProjectsProvider>
        <ContextConsumer />
      </ProjectsProvider>,
    );

    await waitFor(() => expect(mockedProjectApi.getTasks).toHaveBeenCalledTimes(1));

    await act(async () => {
      screen.getByRole('button', { name: 'Search' }).click();
    });

    await waitFor(() => expect(mockedProjectApi.getTasks).toHaveBeenCalledTimes(2));
    expect(signals[0].aborted).toBe(true);
    expect(signals[1].aborted).toBe(false);

    await act(async () => {
      firstRequest.resolve(createTasksResponse('Old task'));
      secondRequest.resolve(createTasksResponse('Fresh task'));
    });

    await waitFor(() => expect(screen.getByTestId('task')).toHaveTextContent('Fresh task'));
    expect(screen.getByTestId('task')).not.toHaveTextContent('Old task');
    expect(screen.getByTestId('tasks-loading')).toHaveTextContent('idle');
  });
});
