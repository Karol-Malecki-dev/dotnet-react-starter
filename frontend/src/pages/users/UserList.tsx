import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { adminApi } from '../../services/api';
import { useAuth } from '../../hooks/useAuth';
import {
  AdminUserRole,
  type AdminUserDetailsDto,
  type AdminUserFilterRequestDto,
  type AdminUserListItemDto,
} from '../../types';
import { getApiErrorMessage } from '../../utils/helpers';

type RoleFilter = 'all' | AdminUserRole;
type ToggleFilter = 'all' | 'true' | 'false';

function formatUserRoleLabel(role: string | AdminUserRole) {
  return role === AdminUserRole.Admin || role === 'Admin' ? 'Admin' : 'User';
}

function getNextRole(role: string) {
  return role === 'Admin' ? AdminUserRole.User : AdminUserRole.Admin;
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat('en-GB', {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value));
}

export default function UserList() {
  const { user } = useAuth();
  const [users, setUsers] = useState<AdminUserListItemDto[]>([]);
  const [selectedUser, setSelectedUser] = useState<AdminUserDetailsDto | null>(null);
  const [selectedUserLoading, setSelectedUserLoading] = useState(false);
  const [selectedUserError, setSelectedUserError] = useState<string | null>(null);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [emailFilter, setEmailFilter] = useState('');
  const [roleFilter, setRoleFilter] = useState<RoleFilter>('all');
  const [activeFilter, setActiveFilter] = useState<ToggleFilter>('all');
  const [emailConfirmedFilter, setEmailConfirmedFilter] = useState<ToggleFilter>('all');
  const [twoFactorFilter, setTwoFactorFilter] = useState<ToggleFilter>('all');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionMessage, setActionMessage] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [busyAction, setBusyAction] = useState<string | null>(null);
  const isAdmin = user?.role === 'Admin';

  const loadUsers = useCallback(
    async (preserveSelection = false) => {
      setLoading(true);
      setError(null);

      if (!preserveSelection) {
        setSelectedUser(null);
        setSelectedUserError(null);
      }

      try {
        const request: AdminUserFilterRequestDto = {
          pageNumber,
          pageSize,
          emails: emailFilter.trim() ? [emailFilter.trim()] : undefined,
          roles: roleFilter === 'all' ? undefined : [roleFilter],
          isActive: activeFilter === 'all' ? undefined : activeFilter === 'true',
          isEmailConfirmed: emailConfirmedFilter === 'all' ? undefined : emailConfirmedFilter === 'true',
          isTwoFactorEnabled: twoFactorFilter === 'all' ? undefined : twoFactorFilter === 'true',
        };

        const response = await adminApi.getUsers(request);
        setUsers(response.data ?? []);
      } catch (caughtError) {
        setError(
          getApiErrorMessage(caughtError, {
            defaultMessage: 'Failed to load users',
          }),
        );
      } finally {
        setLoading(false);
      }
    },
    [activeFilter, emailFilter, emailConfirmedFilter, pageNumber, pageSize, roleFilter, twoFactorFilter],
  );

  const loadUserDetails = async (id: string) => {
    setSelectedUserLoading(true);
    setSelectedUserError(null);

    try {
      const response = await adminApi.getUserDetailsById(id);
      setSelectedUser(response.data ?? null);
    } catch (caughtError) {
      setSelectedUserError(
        getApiErrorMessage(caughtError, {
          defaultMessage: 'Failed to load user details',
        }),
      );
    } finally {
      setSelectedUserLoading(false);
    }
  };

  useEffect(() => {
    void loadUsers();
    // Reset the selected record when the directory filters change.
    setSelectedUser(null);
    setSelectedUserError(null);
  }, [loadUsers]);

  const handleRefresh = async () => {
    await loadUsers(true);
  };

  const handleRoleChange = async (id: string, currentRole: string) => {
    const nextRole = getNextRole(currentRole);
    const actionKey = `${id}:role`;
    setBusyAction(actionKey);
    setActionMessage(null);
    setActionError(null);

    try {
      const response = await adminApi.updateUserRole(id, nextRole);
      if (response.data) {
        setSelectedUser((current) => (current?.id === id ? response.data : current));
      }
      setActionMessage(`Role updated to ${formatUserRoleLabel(nextRole)}.`);
      await loadUsers(true);
    } catch (caughtError) {
      setActionError(
        getApiErrorMessage(caughtError, {
          defaultMessage: 'Failed to update role',
        }),
      );
    } finally {
      setBusyAction(null);
    }
  };

  const handleToggleActive = async (userItem: AdminUserListItemDto) => {
    const actionKey = `${userItem.id}:active`;
    setBusyAction(actionKey);
    setActionMessage(null);
    setActionError(null);

    try {
      const response = userItem.isActive ? await adminApi.deactivateUser(userItem.id) : await adminApi.activateUser(userItem.id);
      if (response.data) {
        setSelectedUser((current) => (current?.id === userItem.id ? response.data : current));
      }
      setActionMessage(userItem.isActive ? 'User deactivated.' : 'User activated.');
      await loadUsers(true);
    } catch (caughtError) {
      setActionError(
        getApiErrorMessage(caughtError, {
          defaultMessage: 'Failed to update active state',
        }),
      );
    } finally {
      setBusyAction(null);
    }
  };

  const handleDelete = async (userItem: AdminUserListItemDto) => {
    if (!window.confirm(`Delete ${userItem.displayName}?`)) {
      return;
    }

    const actionKey = `${userItem.id}:delete`;
    setBusyAction(actionKey);
    setActionMessage(null);
    setActionError(null);

    try {
      await adminApi.deleteUser(userItem.id);
      if (selectedUser?.id === userItem.id) {
        setSelectedUser(null);
      }
      setActionMessage('User deleted.');
      await loadUsers(false);
    } catch (caughtError) {
      setActionError(
        getApiErrorMessage(caughtError, {
          defaultMessage: 'Failed to delete user',
        }),
      );
    } finally {
      setBusyAction(null);
    }
  };

  return (
    <section className="page-shell">
      <div className="page-shell__header">
        <div className="stack stack--tight">
          <p className="eyebrow">Admin only</p>
          <h1>User directory</h1>
          <p className="page-note">
            The backend admin controller exposes filtering, role changes, activation, deletion, and detail lookups. This page consumes those endpoints directly.
          </p>
        </div>
        <div className="hero__actions">
          <Link className="button button--ghost" to="/admin">
            Back to admin panel
          </Link>
          {isAdmin ? <div className="role-badge role-badge--admin">Admin access</div> : <div className="role-badge">Read-only</div>}
        </div>
      </div>

      <article className="card stack">
        <div className="stack stack--tight">
          <p className="eyebrow">Filters</p>
          <h2>Targeted search</h2>
          <p className="page-note">Filter by email, role, activation state, email confirmation, and two-factor status.</p>
        </div>

        <div className="grid grid--2">
          <label className="field">
            <span className="field__label">Email</span>
            <input
              type="email"
              value={emailFilter}
              onChange={(event) => {
                setPageNumber(1);
                setEmailFilter(event.target.value);
              }}
              placeholder="user@example.com"
            />
          </label>

          <label className="field">
            <span className="field__label">Role</span>
            <select
              value={roleFilter}
              onChange={(event) => {
                setPageNumber(1);
                setRoleFilter(event.target.value === 'all' ? 'all' : Number(event.target.value) as AdminUserRole);
              }}
            >
              <option value="all">All roles</option>
              <option value={AdminUserRole.User}>User</option>
              <option value={AdminUserRole.Admin}>Admin</option>
            </select>
          </label>

          <label className="field">
            <span className="field__label">Active state</span>
            <select
              value={activeFilter}
              onChange={(event) => {
                setPageNumber(1);
                setActiveFilter(event.target.value as ToggleFilter);
              }}
            >
              <option value="all">All</option>
              <option value="true">Active</option>
              <option value="false">Inactive</option>
            </select>
          </label>

          <label className="field">
            <span className="field__label">Email confirmation</span>
            <select
              value={emailConfirmedFilter}
              onChange={(event) => {
                setPageNumber(1);
                setEmailConfirmedFilter(event.target.value as ToggleFilter);
              }}
            >
              <option value="all">All</option>
              <option value="true">Confirmed</option>
              <option value="false">Unconfirmed</option>
            </select>
          </label>

          <label className="field">
            <span className="field__label">Two-factor</span>
            <select
              value={twoFactorFilter}
              onChange={(event) => {
                setPageNumber(1);
                setTwoFactorFilter(event.target.value as ToggleFilter);
              }}
            >
              <option value="all">All</option>
              <option value="true">Enabled</option>
              <option value="false">Disabled</option>
            </select>
          </label>

          <label className="field">
            <span className="field__label">Page size</span>
            <select
              value={pageSize}
              onChange={(event) => {
                setPageNumber(1);
                setPageSize(Number(event.target.value));
              }}
            >
              <option value={5}>5</option>
              <option value={10}>10</option>
              <option value={20}>20</option>
            </select>
          </label>
        </div>

        <div className="toolbar">
          <div className="toolbar__group">
            <button className="button button--ghost" type="button" disabled={pageNumber === 1 || loading} onClick={() => setPageNumber((page) => Math.max(1, page - 1))}>
              Previous
            </button>
            <span className="role-badge">Page {pageNumber}</span>
            <button className="button button--ghost" type="button" disabled={loading || users.length < pageSize} onClick={() => setPageNumber((page) => page + 1)}>
              Next
            </button>
          </div>

          <button className="button button--ghost" type="button" onClick={() => void handleRefresh()} disabled={loading}>
            Refresh
          </button>
        </div>
      </article>

      {loading ? <div className="page-state">Loading users...</div> : null}
      {error ? <p className="form__error">{error}</p> : null}
      {actionError ? <p className="form__error">{actionError}</p> : null}
      {actionMessage ? <p className="form__success">{actionMessage}</p> : null}

      <div className="grid grid--2">
        <div className="stack">
          {users.length === 0 && !loading ? <div className="page-state">No users found for the current filters.</div> : null}

          {users.map((currentUser) => {
            const isRoleBusy = busyAction === `${currentUser.id}:role`;
            const isActiveBusy = busyAction === `${currentUser.id}:active`;
            const isDeleteBusy = busyAction === `${currentUser.id}:delete`;

            return (
              <article className="card stack stack--tight" key={currentUser.id}>
                <div className="page-shell__header">
                  <div className="stack stack--tight">
                    <h2>{currentUser.displayName}</h2>
                    <p className="page-note">{currentUser.email}</p>
                  </div>
                  <span className={`role-badge ${currentUser.role === 'Admin' ? 'role-badge--admin' : ''}`}>{currentUser.role}</span>
                </div>

                <div className="toolbar" style={{ alignItems: 'center' }}>
                  <div className="toolbar__group">
                    <span className="role-badge">{currentUser.isActive ? 'Active' : 'Inactive'}</span>
                    <span className="role-badge">{currentUser.isEmailConfirmed ? 'Email confirmed' : 'Email pending'}</span>
                  </div>
                  <button className="button button--ghost" type="button" onClick={() => void loadUserDetails(currentUser.id)} disabled={selectedUserLoading}>
                    View details
                  </button>
                </div>

                <div className="hero__actions">
                  <button className="button button--ghost" type="button" disabled={isRoleBusy} onClick={() => void handleRoleChange(currentUser.id, currentUser.role)}>
                    {currentUser.role === 'Admin' ? 'Set User' : 'Set Admin'}
                  </button>
                  <button className="button button--ghost" type="button" disabled={isActiveBusy} onClick={() => void handleToggleActive(currentUser)}>
                    {currentUser.isActive ? 'Deactivate' : 'Activate'}
                  </button>
                  <button className="button button--danger" type="button" disabled={isDeleteBusy} onClick={() => void handleDelete(currentUser)}>
                    Delete
                  </button>
                </div>

                <p className="page-note">Created at {formatDate(currentUser.createdAt)}</p>
              </article>
            );
          })}
        </div>

        <aside className="card stack stack--tight" aria-labelledby="selected-user-heading">
          <div className="stack stack--tight">
            <p className="eyebrow">Details</p>
            <h2 id="selected-user-heading">Selected user</h2>
            <p className="page-note">The backend exposes a full admin view, but the MVP keeps address editing out of scope.</p>
          </div>

          {selectedUserLoading ? <div className="page-state">Loading user details...</div> : null}
          {selectedUserError ? <p className="form__error">{selectedUserError}</p> : null}

          {selectedUser ? (
            <div className="stack stack--tight">
              <h3>{selectedUser.displayName}</h3>
              <p><strong>Email:</strong> {selectedUser.email}</p>
              <p><strong>Role:</strong> {formatUserRoleLabel(selectedUser.role)}</p>
              <p><strong>Status:</strong> {selectedUser.isActive ? 'Active' : 'Inactive'}</p>
              <p><strong>Email confirmed:</strong> {selectedUser.isEmailConfirmed ? 'Yes' : 'No'}</p>
              <p><strong>Two-factor:</strong> {selectedUser.isTwoFactorEnabled ? 'Enabled' : 'Disabled'}</p>
              <p><strong>Avatar URL:</strong> {selectedUser.avatarUrl ?? 'Not set'}</p>
              <p><strong>Created at:</strong> {formatDate(selectedUser.createdAt)}</p>
              <p className="page-note">Address is available in the backend DTO but not surfaced in this frontend MVP.</p>
            </div>
          ) : (
            <p className="page-note">Select a user card to inspect details.</p>
          )}
        </aside>
      </div>
    </section>
  );
}
