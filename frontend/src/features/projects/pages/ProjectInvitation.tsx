import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useProjects } from '../context/ProjectsContext';

export default function ProjectInvitation() {
  const [searchParams] = useSearchParams();
  const { acceptProjectInvitation, declineProjectInvitation } = useProjects();
  const [status, setStatus] = useState<'idle' | 'accepted' | 'declined'>('idle');
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const token = searchParams.get('token');

  const respond = async (response: 'accepted' | 'declined') => {
    if (!token) return;
    setSaving(true);
    setError(null);
    try {
      if (response === 'accepted') await acceptProjectInvitation(token);
      else await declineProjectInvitation(token);
      setStatus(response);
    } catch (caughtError) {
      setError(caughtError instanceof Error ? caughtError.message : 'Unable to respond to this invitation');
    } finally {
      setSaving(false);
    }
  };

  return (
    <section className="page-shell">
      <div className="form-shell">
        <h1>Project invitation</h1>
        {!token ? <p className="form__error">This invitation link is invalid.</p> : status === 'accepted' ? <p className="form__success">You joined the project.</p> : status === 'declined' ? <p className="page-note">You declined this invitation.</p> : <>
          <p className="page-note">Choose whether to join this project with the role selected by its owner.</p>
          {error ? <p className="form__error">{error}</p> : null}
          <div className="hero__actions">
            <button className="button" type="button" disabled={saving} onClick={() => void respond('accepted')}>Accept invitation</button>
            <button className="button button--danger" type="button" disabled={saving} onClick={() => void respond('declined')}>Decline</button>
          </div>
        </>}
      </div>
    </section>
  );
}
