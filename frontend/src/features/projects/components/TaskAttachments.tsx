import { useRef, useState, type FormEvent } from 'react';
import type { ProjectTaskAttachmentDto } from '../types';

function formatAttachmentSize(sizeBytes: number) {
  if (sizeBytes < 1024) return `${sizeBytes} B`;
  if (sizeBytes < 1024 * 1024) return `${(sizeBytes / 1024).toFixed(1)} KB`;
  return `${(sizeBytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function TaskAttachments({ taskId, attachments, loading, canUpload, canDelete, onUpload, onDownload, onDelete }: { taskId: string; attachments?: ProjectTaskAttachmentDto[]; loading: boolean; canUpload: boolean; canDelete: (uploadedByUserId: string) => boolean; onUpload: (file: File) => Promise<unknown>; onDownload: (attachmentId: string) => Promise<Blob>; onDelete: (attachmentId: string) => Promise<void> }) {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [saving, setSaving] = useState(false);
  const [downloadingId, setDownloadingId] = useState<string | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [validationError, setValidationError] = useState<string | null>(null);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (!selectedFile) return;
    if (selectedFile.size > 10 * 1024 * 1024) {
      setValidationError('Attachments must be 10 MB or smaller.');
      return;
    }

    setSaving(true);
    setValidationError(null);
    try {
      await onUpload(selectedFile);
      setSelectedFile(null);
      if (fileInputRef.current) fileInputRef.current.value = '';
    } finally {
      setSaving(false);
    }
  };

  const download = async (attachment: ProjectTaskAttachmentDto) => {
    setDownloadingId(attachment.id);
    try {
      const blob = await onDownload(attachment.id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = attachment.originalFileName;
      link.click();
      URL.revokeObjectURL(url);
    } finally {
      setDownloadingId(null);
    }
  };

  const remove = async (attachmentId: string) => {
    setDeletingId(attachmentId);
    try {
      await onDelete(attachmentId);
    } finally {
      setDeletingId(null);
    }
  };

  return (
    <section className="task-attachments" aria-label="Task attachments">
      <h3>Attachments</h3>
      {loading ? <p className="page-note">Loading attachments...</p> : attachments?.length ? <div className="task-attachments__list">{attachments.map((attachment) => <article className="task-attachments__item" key={attachment.id}><div><strong>{attachment.originalFileName}</strong><small>{attachment.uploaderDisplayName} · {new Date(attachment.createdAt).toLocaleString()} · {formatAttachmentSize(attachment.sizeBytes)}</small></div><div className="task-attachments__actions"><button className="button button--ghost" type="button" disabled={downloadingId === attachment.id} onClick={() => void download(attachment)}>{downloadingId === attachment.id ? 'Downloading...' : 'Download'}</button>{canDelete(attachment.uploadedByUserId) ? <button className="button button--danger" type="button" disabled={deletingId === attachment.id} onClick={() => void remove(attachment.id)}>{deletingId === attachment.id ? 'Deleting...' : 'Delete'}</button> : null}</div></article>)}</div> : <p className="page-note">No attachments yet.</p>}
      {canUpload ? <form className="task-attachments__form" onSubmit={submit}><label className="field__label" htmlFor={`task-attachment-${taskId}`}>Choose a file</label><input ref={fileInputRef} id={`task-attachment-${taskId}`} type="file" accept=".pdf,.png,.jpg,.jpeg,.docx,.xlsx,.txt" onChange={(event) => { setSelectedFile(event.target.files?.[0] ?? null); setValidationError(null); }} /><button className="button" type="submit" disabled={!selectedFile || saving}>{saving ? 'Uploading...' : 'Upload attachment'}</button>{validationError ? <p className="field__error" role="alert">{validationError}</p> : null}</form> : <p className="page-note">Viewers can download attachments but cannot upload them.</p>}
    </section>
  );
}

