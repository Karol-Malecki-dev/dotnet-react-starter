import { ProjectTaskStatus, type ProjectTaskDto } from '../types';
import { priorityLabels, statusLabels } from './projectLabels';

export function TaskBoard({ tasks, canManageTask, onStatusChange }: { tasks: ProjectTaskDto[]; canManageTask: (task: ProjectTaskDto) => boolean; onStatusChange: (taskId: string, status: ProjectTaskStatus) => Promise<void> }) {
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
