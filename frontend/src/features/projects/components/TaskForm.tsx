import { useState, type FormEvent } from 'react';
import { ProjectTaskPriority, type CreateProjectTaskRequest, type ProjectMemberDto, type ProjectTaskDto } from '../types';
import { priorityLabels } from './projectLabels';

export function TaskForm({ onSubmit, initialTask, assignmentEnabled, members }: { onSubmit: (request: CreateProjectTaskRequest) => Promise<unknown>; initialTask?: ProjectTaskDto; assignmentEnabled?: boolean; members: ProjectMemberDto[] }) {
  const [title, setTitle] = useState(initialTask?.title ?? '');
  const [description, setDescription] = useState(initialTask?.description ?? '');
  const [priority, setPriority] = useState(initialTask?.priority ?? ProjectTaskPriority.Normal);
  const [dueDate, setDueDate] = useState(initialTask?.dueDate?.slice(0, 10) ?? '');
  const [assignedUserId, setAssignedUserId] = useState(initialTask?.assignedUserId ?? '');
  const [labels, setLabels] = useState(initialTask?.labels.join(', ') ?? '');
  const [saving, setSaving] = useState(false);

  const submit = async (event: FormEvent) => {
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
