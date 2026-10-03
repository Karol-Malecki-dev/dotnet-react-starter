import { useState } from 'react';
import { ProjectMemberRole, type ProjectMemberDto } from '../types';

export function MemberPanel({ members, availableMembers, ownerId, onAdd, onRemove, onRoleChange }: { members: ProjectMemberDto[]; availableMembers: { id: string; displayName: string; email: string }[]; ownerId: string; onAdd: (userId: string) => Promise<void>; onRemove: (userId: string) => Promise<void>; onRoleChange?: (userId: string, role: ProjectMemberRole) => Promise<void> }) {
  const [selectedUserId, setSelectedUserId] = useState('');
  const [saving, setSaving] = useState(false);

  const add = async () => {
    if (!selectedUserId) return;
    setSaving(true);
    try {
      await onAdd(selectedUserId);
      setSelectedUserId('');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="card">
      <h2>Project members</h2>
      <div className="member-list">
        {members.map((member) => (
          <div className="member-list__item" key={member.userId}>
            <span><strong>{member.displayName}</strong><small>{member.email}</small></span>
            {member.userId !== ownerId ? <><select aria-label={`Role for ${member.displayName}`} value={member.role ?? ProjectMemberRole.Member} onChange={(event) => { if (onRoleChange) void onRoleChange(member.userId, Number(event.target.value) as ProjectMemberRole); }}><option value={ProjectMemberRole.Member}>Member</option><option value={ProjectMemberRole.Viewer}>Viewer</option></select><button className="button button--danger" type="button" onClick={() => void onRemove(member.userId)}>Remove</button></> : <span className="role-badge">Owner</span>}
          </div>
        ))}
      </div>
      <div className="member-add">
        <select aria-label="Available project users" value={selectedUserId} onChange={(event) => setSelectedUserId(event.target.value)}>
          <option value="">Select user to add</option>
          {availableMembers.map((user) => <option key={user.id} value={user.id}>{user.displayName} ({user.email})</option>)}
        </select>
        <button className="button" type="button" disabled={!selectedUserId || saving} onClick={() => void add()}>{saving ? 'Adding...' : 'Add member'}</button>
      </div>
    </div>
  );
}
