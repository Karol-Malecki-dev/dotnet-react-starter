import { useState, type FormEvent } from 'react';
import type { ProjectDto } from '../types';

export function ProjectForm({ project, onSubmit, onCancel }: {
  project?: ProjectDto;
  onSubmit: (name: string, description: string) => Promise<void>;
  onCancel?: () => void;
}) {
  const [name, setName] = useState(project?.name ?? '');
  const [description, setDescription] = useState(project?.description ?? '');
  const [saving, setSaving] = useState(false);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (!name.trim()) return;
    setSaving(true);
    try {
      await onSubmit(name.trim(), description.trim());
      if (!project) {
        setName('');
        setDescription('');
      }
    } finally {
      setSaving(false);
    }
  };

  return (
    <form className="form" onSubmit={submit}>
      <div className="field"><label className="field__label" htmlFor="project-name">Name</label><input id="project-name" value={name} onChange={(event) => setName(event.target.value)} required /></div>
      <div className="field"><label className="field__label" htmlFor="project-description">Description</label><textarea id="project-description" value={description} onChange={(event) => setDescription(event.target.value)} rows={3} /></div>
      <div className="hero__actions">
        <button className="button" type="submit" disabled={saving}>{saving ? 'Saving...' : project ? 'Save changes' : 'Create project'}</button>
        {onCancel ? <button className="button button--ghost" type="button" onClick={onCancel}>Cancel</button> : null}
      </div>
    </form>
  );
}
