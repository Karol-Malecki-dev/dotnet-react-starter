import { useState } from 'react';
import { ProjectTaskStatus, type ProjectTaskDto } from '../types';
import { priorityLabels, statusLabels } from './projectLabels';

export function TaskItem({ task, canManage, discussionOpen, attachmentsOpen, onToggleDiscussion, onToggleAttachments, onStatusChange, onDelete, onEdit }: { task: ProjectTaskDto; canManage: boolean; discussionOpen: boolean; attachmentsOpen: boolean; onToggleDiscussion: () => void; onToggleAttachments: () => void; onStatusChange: (status: ProjectTaskStatus) => Promise<void>; onDelete: () => Promise<void>; onEdit: () => void }) {
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
