import { FormEvent, useEffect, useState } from 'react';
import { useAuth } from '../hooks/useAuth';
import { authApi, notificationApi, userApi } from '../services/api';
import type { AuthUser, UpdateUserRequest, UserSecurity } from '../types';
import { getApiErrorMessage } from '../utils/helpers';

function splitDisplayName(displayName: string) {
  const parts = displayName.trim().split(/\s+/).filter(Boolean);
  return {
    firstName: parts[0] ?? '',
    lastName: parts.slice(1).join(' '),
  };
}

function createProfileState(user: AuthUser | null) {
  const parsedName = splitDisplayName(user?.displayName ?? '');

  return {
    firstName: user?.firstName ?? parsedName.firstName,
    lastName: user?.lastName ?? parsedName.lastName,
    email: user?.email ?? '',
    avatarUrl: user?.avatarUrl ?? '',
  };
}

export default function Profile() {
  const { user, tokens, updateProfile, changePassword } = useAuth();
  const [profile, setProfile] = useState(() => createProfileState(user));
  const [passwords, setPasswords] = useState({
    currentPassword: '',
    newPassword: '',
    confirmPassword: '',
  });
  const [savingProfile, setSavingProfile] = useState(false);
  const [changingPassword, setChangingPassword] = useState(false);
  const [profileMessage, setProfileMessage] = useState<string | null>(null);
  const [profileError, setProfileError] = useState<string | null>(null);
  const [passwordMessage, setPasswordMessage] = useState<string | null>(null);
  const [passwordError, setPasswordError] = useState<string | null>(null);
  const [security, setSecurity] = useState<UserSecurity | null>(null);
  const [loadingSecurity, setLoadingSecurity] = useState(false);
  const [updatingTwoFactor, setUpdatingTwoFactor] = useState(false);
  const [resendingConfirmation, setResendingConfirmation] = useState(false);
  const [securityMessage, setSecurityMessage] = useState<string | null>(null);
  const [securityError, setSecurityError] = useState<string | null>(null);
  const [isNotificationEmailEnabled, setIsNotificationEmailEnabled] = useState<boolean | null>(null);
  const [loadingNotificationPreference, setLoadingNotificationPreference] = useState(false);
  const [updatingNotificationPreference, setUpdatingNotificationPreference] = useState(false);
  const [notificationPreferenceError, setNotificationPreferenceError] = useState<string | null>(null);

  useEffect(() => {
    setProfile(createProfileState(user));
  }, [user]);

  useEffect(() => {
    let isCurrent = true;

    const loadNotificationPreference = async () => {
      if (!user) {
        setIsNotificationEmailEnabled(null);
        return;
      }

      setLoadingNotificationPreference(true);
      setNotificationPreferenceError(null);

      try {
        const response = await notificationApi.getEmailPreference();
        if (!response.data) {
          throw new Error('Notification preference response missing data');
        }

        if (isCurrent) {
          setIsNotificationEmailEnabled(response.data.isEmailEnabled);
        }
      } catch (caughtError) {
        if (isCurrent) {
          setNotificationPreferenceError(
            getApiErrorMessage(caughtError, {
              defaultMessage: 'Unable to load notification settings right now.',
            }),
          );
        }
      } finally {
        if (isCurrent) {
          setLoadingNotificationPreference(false);
        }
      }
    };

    void loadNotificationPreference();

    return () => {
      isCurrent = false;
    };
  }, [user]);

  useEffect(() => {
    let isCurrent = true;

    const loadSecurity = async () => {
      if (!user) {
        setSecurity(null);
        return;
      }

      setLoadingSecurity(true);
      setSecurityError(null);

      try {
        const response = await userApi.getUserSecurity();
        if (isCurrent) {
          if (!response.data) {
            throw new Error('Security response missing data');
          }
          setSecurity(response.data);
        }
      } catch (caughtError) {
        if (isCurrent) {
          setSecurityError(
            getApiErrorMessage(caughtError, {
              defaultMessage: 'Unable to load account security right now.',
            }),
          );
        }
      } finally {
        if (isCurrent) {
          setLoadingSecurity(false);
        }
      }
    };

    void loadSecurity();

    return () => {
      isCurrent = false;
    };
  }, [user]);

  const handleProfileSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const firstName = profile.firstName.trim();
    const lastName = profile.lastName.trim();
    const email = profile.email.trim();
    const avatarUrl = profile.avatarUrl.trim();

    if (!firstName || !lastName) {
      setProfileError('First name and last name are required.');
      return;
    }

    if (!email) {
      setProfileError('Email is required.');
      return;
    }

    setSavingProfile(true);
    setProfileMessage(null);
    setProfileError(null);

    try {
      const request: UpdateUserRequest = {
        firstName,
        lastName,
        email,
        avatarUrl: avatarUrl || null,
      };

      await updateProfile(request);
      setProfileMessage('Profile updated.');
    } catch (caughtError) {
      setProfileError(
        getApiErrorMessage(caughtError, {
          defaultMessage: 'Unable to update profile right now.',
        }),
      );
    } finally {
      setSavingProfile(false);
    }
  };

  const handlePasswordSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    if (!passwords.currentPassword || !passwords.newPassword) {
      setPasswordError('Current password and new password are required.');
      return;
    }

    if (passwords.newPassword.length < 8) {
      setPasswordError('New password must be at least 8 characters long.');
      return;
    }

    if (passwords.newPassword !== passwords.confirmPassword) {
      setPasswordError('New password confirmation does not match.');
      return;
    }

    setChangingPassword(true);
    setPasswordMessage(null);
    setPasswordError(null);

    try {
      await changePassword(passwords.currentPassword, passwords.newPassword);
      setPasswords({ currentPassword: '', newPassword: '', confirmPassword: '' });
      setPasswordMessage('Password changed.');
    } catch (caughtError) {
      setPasswordError(
        getApiErrorMessage(caughtError, {
          defaultMessage: 'Unable to change password right now.',
        }),
      );
    } finally {
      setChangingPassword(false);
    }
  };

  const handleTwoFactorChange = async (enabled: boolean) => {
    if (!security || (enabled && !security.isEmailConfirmed)) {
      return;
    }

    setUpdatingTwoFactor(true);
    setSecurityMessage(null);
    setSecurityError(null);

    try {
      const response = await userApi.updateTwoFactorPreference({ enable: enabled });
      if (!response.data) {
        throw new Error('Security update response missing data');
      }
      setSecurity(response.data);
      setSecurityMessage(response.message || 'Security settings updated.');
    } catch (caughtError) {
      setSecurityError(
        getApiErrorMessage(caughtError, {
          defaultMessage: 'Unable to update two-factor authentication right now.',
        }),
      );
    } finally {
      setUpdatingTwoFactor(false);
    }
  };

  const handleResendConfirmation = async () => {
    if (!security?.email) {
      return;
    }

    setResendingConfirmation(true);
    setSecurityMessage(null);
    setSecurityError(null);

    try {
      const response = await authApi.resendConfirmation({ email: security.email });
      setSecurityMessage(response.message || 'Confirmation email sent.');
    } catch (caughtError) {
      setSecurityError(
        getApiErrorMessage(caughtError, {
          defaultMessage: 'Unable to resend the confirmation email right now.',
        }),
      );
    } finally {
      setResendingConfirmation(false);
    }
  };

  const handleNotificationEmailPreferenceChange = async (isEmailEnabled: boolean) => {
    setUpdatingNotificationPreference(true);
    setNotificationPreferenceError(null);

    try {
      const response = await notificationApi.updateEmailPreference({ isEmailEnabled });
      if (!response.data) {
        throw new Error('Notification preference update response missing data');
      }
      setIsNotificationEmailEnabled(response.data.isEmailEnabled);
    } catch (caughtError) {
      setNotificationPreferenceError(
        getApiErrorMessage(caughtError, {
          defaultMessage: 'Unable to update notification settings right now.',
        }),
      );
    } finally {
      setUpdatingNotificationPreference(false);
    }
  };

  return (
    <section className="page-shell">
      <div className="page-shell__header">
        <div className="stack stack--tight">
          <p className="eyebrow">Account</p>
          <h1>Profile</h1>
          <p className="page-note">Manage your account details, avatar URL, and password from the authenticated session.</p>
        </div>
      </div>
      {user ? (
        <div className="grid grid--2">
          <article className="card stack stack--tight">
            <h2>Session details</h2>
            <p><strong>ID:</strong> {user.id}</p>
            <p><strong>Name:</strong> {user.displayName}</p>
            <p><strong>Email:</strong> {user.email}</p>
            <p><strong>Avatar URL:</strong> {user.avatarUrl || 'Not set'}</p>
            <p><strong>Role:</strong> {user.role}</p>
            <p><strong>Access expires in:</strong> {tokens?.expiresIn ?? 0}s</p>
          </article>

          <article className="card stack">
            <div className="stack stack--tight">
              <h2>Profile details</h2>
              <p className="page-note">This updates your profile through the dedicated `users/me` endpoint and keeps the local session in sync.</p>
            </div>
            <form className="form" onSubmit={handleProfileSubmit}>
              <label className="field">
                <span className="field__label">First name</span>
                <input
                  value={profile.firstName}
                  onChange={(event) => setProfile((current) => ({ ...current, firstName: event.target.value }))}
                  autoComplete="given-name"
                  required
                />
              </label>
              <label className="field">
                <span className="field__label">Last name</span>
                <input
                  value={profile.lastName}
                  onChange={(event) => setProfile((current) => ({ ...current, lastName: event.target.value }))}
                  autoComplete="family-name"
                  required
                />
              </label>
              <label className="field">
                <span className="field__label">Email</span>
                <input
                  type="email"
                  value={profile.email}
                  onChange={(event) => setProfile((current) => ({ ...current, email: event.target.value }))}
                  autoComplete="email"
                  required
                />
              </label>
              <label className="field">
                <span className="field__label">Avatar URL</span>
                <input
                  type="url"
                  value={profile.avatarUrl}
                  onChange={(event) => setProfile((current) => ({ ...current, avatarUrl: event.target.value }))}
                  placeholder="https://example.com/avatar.png"
                  autoComplete="url"
                />
              </label>
              {profileMessage ? <p className="form__success">{profileMessage}</p> : null}
              {profileError ? <p className="form__error">{profileError}</p> : null}
              <button className="button" type="submit" disabled={savingProfile}>
                {savingProfile ? 'Saving...' : 'Save profile'}
              </button>
            </form>
          </article>

          <article className="card stack">
            <div className="stack stack--tight">
              <h2>Change password</h2>
              <p className="page-note">Your current password is required before the backend accepts a new one.</p>
            </div>
            <form className="form" onSubmit={handlePasswordSubmit}>
              <label className="field">
                <span className="field__label">Current password</span>
                <input
                  type="password"
                  value={passwords.currentPassword}
                  onChange={(event) => setPasswords((current) => ({ ...current, currentPassword: event.target.value }))}
                  autoComplete="current-password"
                  required
                />
              </label>
              <label className="field">
                <span className="field__label">New password</span>
                <input
                  type="password"
                  value={passwords.newPassword}
                  onChange={(event) => setPasswords((current) => ({ ...current, newPassword: event.target.value }))}
                  autoComplete="new-password"
                  required
                />
              </label>
              <label className="field">
                <span className="field__label">Confirm new password</span>
                <input
                  type="password"
                  value={passwords.confirmPassword}
                  onChange={(event) => setPasswords((current) => ({ ...current, confirmPassword: event.target.value }))}
                  autoComplete="new-password"
                  required
                />
              </label>
              {passwordMessage ? <p className="form__success">{passwordMessage}</p> : null}
              {passwordError ? <p className="form__error">{passwordError}</p> : null}
              <button className="button" type="submit" disabled={changingPassword}>
                {changingPassword ? 'Saving...' : 'Change password'}
              </button>
            </form>
          </article>

          <article className="card stack">
            <div className="stack stack--tight">
              <h2>Account security</h2>
              <p className="page-note">Manage email confirmation and two-factor authentication.</p>
            </div>
            {loadingSecurity ? <p role="status">Loading security settings...</p> : null}
            {securityError ? <p className="form__error" role="alert">{securityError}</p> : null}
            {security ? (
              <div className="stack stack--tight">
                <p>
                  <strong>Email confirmation:</strong>{' '}
                  {security.isEmailConfirmed ? 'Confirmed' : 'Not confirmed'}
                </p>
                {!security.isEmailConfirmed ? (
                  <button
                    className="button button--ghost"
                    type="button"
                    onClick={handleResendConfirmation}
                    disabled={resendingConfirmation}
                  >
                    {resendingConfirmation ? 'Sending...' : 'Resend confirmation'}
                  </button>
                ) : null}
                <label className="field field--inline">
                  <span className="field__label">Two-factor authentication</span>
                  <input
                    type="checkbox"
                    checked={security.isTwoFactorEnabled}
                    onChange={(event) => void handleTwoFactorChange(event.target.checked)}
                    disabled={updatingTwoFactor || !security.isEmailConfirmed}
                  />
                </label>
                {!security.isEmailConfirmed ? (
                  <p className="field__hint">Confirm your email before enabling two-factor authentication.</p>
                ) : null}
                {securityMessage ? <p className="form__success">{securityMessage}</p> : null}
              </div>
            ) : null}
          </article>

          <article className="card stack">
            <div className="stack stack--tight">
              <h2>Notification delivery</h2>
              <p className="page-note">Choose whether in-app project and task notifications are also delivered by email.</p>
            </div>
            {loadingNotificationPreference ? <p role="status">Loading notification settings...</p> : null}
            {notificationPreferenceError ? <p className="form__error" role="alert">{notificationPreferenceError}</p> : null}
            {isNotificationEmailEnabled !== null ? (
              <label className="field field--inline">
                <span className="field__label">Email notifications</span>
                <input
                  type="checkbox"
                  checked={isNotificationEmailEnabled}
                  onChange={(event) => void handleNotificationEmailPreferenceChange(event.target.checked)}
                  disabled={updatingNotificationPreference}
                />
              </label>
            ) : null}
          </article>
        </div>
      ) : (
        <p>Brak sesji użytkownika.</p>
      )}
    </section>
  );
}
