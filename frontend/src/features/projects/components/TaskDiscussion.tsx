import { useState, type FormEvent } from 'react';

export function TaskDiscussion({ taskId, comments, loading, canComment, canDeleteComment, onCreate, onDelete }: { taskId: string; comments?: { id: string; authorUserId: string; authorDisplayName: string; content: string; createdAt: string }[]; loading: boolean; canComment: boolean; canDeleteComment: (authorUserId: string) => boolean; onCreate: (content: string) => Promise<unknown>; onDelete: (commentId: string) => Promise<void> }) {
  const [content, setContent] = useState('');
  const [saving, setSaving] = useState(false);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (!content.trim()) return;
    setSaving(true);
    try {
      await onCreate(content.trim());
      setContent('');
    } finally {
      setSaving(false);
    }
  };

  return (
    <section className="task-discussion" aria-label="Task discussion">
      <h3>Discussion</h3>
      {loading ? <p className="page-note">Loading comments...</p> : comments?.length ? <div className="task-discussion__list">{comments.map((comment) => <article key={comment.id} className="task-discussion__comment"><div><strong>{comment.authorDisplayName}</strong><small>{new Date(comment.createdAt).toLocaleString()}</small></div><p>{comment.content}</p>{canDeleteComment(comment.authorUserId) ? <button className="button button--danger" type="button" onClick={() => void onDelete(comment.id)}>Delete comment</button> : null}</article>)}</div> : <p className="page-note">No comments yet.</p>}
      {canComment ? <form className="task-discussion__form" onSubmit={submit}><label className="field__label" htmlFor={`task-comment-${taskId}`}>Add a comment</label><textarea id={`task-comment-${taskId}`} value={content} onChange={(event) => setContent(event.target.value)} rows={3} maxLength={2000} required /><button className="button" type="submit" disabled={saving}>{saving ? 'Posting...' : 'Post comment'}</button></form> : <p className="page-note">Viewers can read comments but cannot add them.</p>}
    </section>
  );
}
