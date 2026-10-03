import { ProjectTaskPriority, ProjectTaskStatus } from '../types';

export const statusLabels: Record<ProjectTaskStatus, string> = {
  [ProjectTaskStatus.Todo]: 'To do',
  [ProjectTaskStatus.InProgress]: 'In progress',
  [ProjectTaskStatus.Done]: 'Done',
};

export const priorityLabels: Record<ProjectTaskPriority, string> = {
  [ProjectTaskPriority.Low]: 'Low',
  [ProjectTaskPriority.Normal]: 'Normal',
  [ProjectTaskPriority.High]: 'High',
};
