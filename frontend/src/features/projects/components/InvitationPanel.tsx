import { useEffect, useState, type FormEvent } from 'react';
import { ProjectInvitationStatus, ProjectMemberRole } from '../types';

export function InvitationPanel({ projectId, invitations, loading, onLoad, onCreate }: { projectId: string; invitations: { id: string; invitedUserDisplayName: string; invitedUserEmail: string; role: ProjectMemberRole; status: ProjectInvitationStatus; expiresAt: string }[]; loading: boolean; onLoad: (projectId: string) => Promise<void>; onCreate: (request: { email: string; role: ProjectMemberRole }) => Promise<{ token: string }> }) {
  const [email, setEmail] = useState('');
  const [role, setRole] = useState(ProjectMemberRole.Member);
  const [inviteLink, setInviteLink] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => { void onLoad(projectId); }, [onLoad, projectId]);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (!email.trim()) return;
    setSaving(true);
    try {
      const created = await onCreate({ email: email.trim(), role });
      setInviteLink(`${window.location.origin}/project-invitation?token=${encodeURIComponent(created.token)}`);
      setEmail('');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="card invitation-panel">
      <div><h2>Invite to project</h2><p className="page-note">Create a time-limited invitation for an existing account.</p></div>
      <form className="invitation-panel__form" onSubmit={submit}>
        <div className="field"><label className="field__label" htmlFor="invitation-email">Account email</label><input id="invitation-email" type="email" value={email} onChange={(event) => setEmail(event.target.value)} required /></div>
        <div className="field"><label className="field__label" htmlFor="invitation-role">Role</label><select id="invitation-role" value={role} onChange={(event) => setRole(Number(event.target.value) as ProjectMemberRole)}><option value={ProjectMemberRole.Member}>Member</option><option value={ProjectMemberRole.Viewer}>Viewer</option></select></div>
        <button className="button" type="submit" disabled={saving}>{saving ? 'Creating...' : 'Create invitation'}</button>
      </form>
      {inviteLink ? <div className="invitation-panel__link"><label className="field__label" htmlFor="invitation-link">Invitation link</label><input id="invitation-link" value={inviteLink} readOnly /><button className="button button--ghost" type="button" onClick={() => void navigator.clipboard.writeText(inviteLink)}>Copy link</button></div> : null}
      <div><h3>Invitation history</h3>{loading ? <p className="page-note">Loading invitations...</p> : invitations.length === 0 ? <p className="page-note">No invitations sent yet.</p> : <div className="member-list">{invitations.map((invitation) => <div key={invitation.id} className="member-list__item"><span><strong>{invitation.invitedUserDisplayName}</strong><small>{invitation.invitedUserEmail}</small></span><small>{invitation.role === ProjectMemberRole.Member ? 'Member' : 'Viewer'} · {ProjectInvitationStatus[invitation.status]} · expires {new Date(invitation.expiresAt).toLocaleDateString()}</small></div>)}</div>}</div>
    </div>
  );
}
