import { useEffect, useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { useAuth } from '../hooks/useAuth';
import type { LoginFormValues } from '../utils/authSchemas';
import { loginSchema } from '../utils/authSchemas';
import { getApiErrorMessage } from '../utils/helpers';
import { clearPendingTwoFactor, savePendingTwoFactor } from '../utils/pendingTwoFactor';
import { HttpError } from '../services/api';

export default function Login() {
  const { login, loading, error, clearError } = useAuth();
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [retryAfterSeconds, setRetryAfterSeconds] = useState(0);
  const navigate = useNavigate();
  const location = useLocation();
  const from = (location.state as { from?: Location } | null)?.from?.pathname ?? '/dashboard';
  const reason = (location.state as { reason?: string } | null)?.reason;
  const registrationPendingEmail = (location.state as { registrationPendingEmail?: string } | null)?.registrationPendingEmail;

  useEffect(() => {
    if (retryAfterSeconds <= 0) {
      return undefined;
    }

    const timer = window.setInterval(() => {
      setRetryAfterSeconds((remaining) => Math.max(remaining - 1, 0));
    }, 1000);

    return () => window.clearInterval(timer);
  }, [retryAfterSeconds]);
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: {
      email: '',
      password: '',
    },
  });

  const onSubmit = async (values: LoginFormValues) => {
    clearError();
    setSubmitError(null);
    setRetryAfterSeconds(0);

    try {
      const result = await login(values.email, values.password);

      if (result.kind === 'two-factor-required') {
        const pendingChallenge = {
          ...result.challenge,
          fromPath: from,
        };

        savePendingTwoFactor(pendingChallenge);
        navigate('/verify-2fa', {
          replace: true,
          state: {
            challenge: pendingChallenge,
          },
        });
        return;
      }

      clearPendingTwoFactor();
      navigate(from, { replace: true });
    } catch (caughtError) {
      if (caughtError instanceof HttpError && caughtError.status === 429) {
        setRetryAfterSeconds(Math.max(caughtError.retryAfterSeconds ?? 0, 1));
      }

      setSubmitError(
        getApiErrorMessage(caughtError, {
          defaultMessage: 'Unable to sign in. Check your credentials, try again later if access is temporarily blocked, or reset your password.',
          rateLimitMessage: 'Too many login attempts. Please wait a moment and try again.',
        }),
      );
    }
  };

  return (
    <section className="auth-layout">
      <article className="auth-callout">
        <p className="eyebrow">JWT access</p>
        <h1>Sign in to the control panel.</h1>
        <p>
          Access the authenticated area, bootstrap the current session from JWT and complete email-based 2FA when the account requires it.
        </p>
        <ul className="auth-callout__list">
          <li>Automatic redirect back to the protected route you requested.</li>
          <li>Refresh-token based session recovery after page reload.</li>
          <li>Email confirmation is required before the first successful sign-in.</li>
          <li>Consistent error handling for invalid credentials, expired sessions, and 2FA challenges.</li>
        </ul>
      </article>

      <article className="auth-panel">
        <div className="auth-panel__header">
          <p className="eyebrow">Welcome back</p>
          <h2>Log in</h2>
          <p>Use the confirmed account credentials, then enter the email verification code if prompted.</p>
        </div>

        {reason === 'session-expired' ? <p className="form__warning">Your session expired. Please sign in again.</p> : null}
        {reason === 'password-reset' ? (
          <p className="form__success">Your password has been reset. Please sign in with your new password.</p>
        ) : null}
        {registrationPendingEmail ? (
          <p className="form__warning">
            Account created for {registrationPendingEmail}. Confirm the email first, then sign in to receive the 2FA code.
          </p>
        ) : null}

        <form className="form" noValidate onSubmit={handleSubmit(onSubmit)}>
          <label className="field">
            <span className="field__label">Email</span>
            <input
              type="email"
              autoComplete="email"
              placeholder="name@company.com"
              aria-invalid={errors.email ? 'true' : 'false'}
              {...register('email')}
            />
            {errors.email ? <span className="field__error">{errors.email.message}</span> : null}
          </label>
          <label className="field">
            <span className="field__label">Password</span>
            <input
              type="password"
              autoComplete="current-password"
              placeholder="Enter your password"
              aria-invalid={errors.password ? 'true' : 'false'}
              {...register('password')}
            />
            {errors.password ? <span className="field__error">{errors.password.message}</span> : null}
          </label>
          <p className="auth-panel__footer">
            <Link to="/forgot-password">Forgot your password?</Link>
          </p>
          {submitError ?? error ? <p className="form__error" aria-live="assertive">{submitError ?? error}</p> : null}
          {retryAfterSeconds > 0 ? (
            <p className="form__warning" role="status">
              Please wait {retryAfterSeconds} seconds before trying again.
            </p>
          ) : null}
          <button className="button button--block" type="submit" disabled={loading || retryAfterSeconds > 0} aria-disabled={loading || retryAfterSeconds > 0}>
            {loading ? 'Signing in...' : retryAfterSeconds > 0 ? 'Try again later' : 'Sign in'}
          </button>
        </form>

        <p className="auth-panel__footer">
          No account yet? <Link to="/register">Create one here</Link>.
        </p>
      </article>
    </section>
  );
}
