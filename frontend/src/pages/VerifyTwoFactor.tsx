import { useEffect, useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { useAuth } from '../hooks/useAuth';
import type { PendingTwoFactorChallenge } from '../types';
import { clearPendingTwoFactor, loadPendingTwoFactor, savePendingTwoFactor } from '../utils/pendingTwoFactor';
import { getApiErrorMessage } from '../utils/helpers';
import type { TwoFactorFormValues } from '../utils/authSchemas';
import { twoFactorSchema } from '../utils/authSchemas';

interface VerifyTwoFactorLocationState {
  challenge?: PendingTwoFactorChallenge;
}

export default function VerifyTwoFactor() {
  const { verifyTwoFactor, resendTwoFactor, loading, error, clearError } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  const locationChallenge = (location.state as VerifyTwoFactorLocationState | null)?.challenge;
  const [challenge, setChallenge] = useState<PendingTwoFactorChallenge | null>(locationChallenge ?? loadPendingTwoFactor());
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [infoMessage, setInfoMessage] = useState<string | null>(null);
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    if (!challenge) {
      return undefined;
    }

    const interval = window.setInterval(() => setNow(Date.now()), 1000);
    return () => {
      window.clearInterval(interval);
    };
  }, [challenge]);

  const isExpired = challenge ? now >= new Date(challenge.expiresAt).getTime() : false;

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<TwoFactorFormValues>({
    resolver: zodResolver(twoFactorSchema),
    defaultValues: {
      code: '',
    },
  });

  if (!challenge) {
    return (
      <section className="auth-layout">
        <article className="auth-callout">
          <p className="eyebrow">Two-factor verification</p>
          <h1>No active verification challenge.</h1>
          <p>Sign in again to request a fresh email code.</p>
        </article>

        <article className="auth-panel">
          <div className="auth-panel__header">
            <p className="eyebrow">Challenge missing</p>
            <h2>Start over</h2>
            <p>The verification step needs an active challenge from the login endpoint.</p>
          </div>

          <Link className="button button--block" to="/login">
            Return to sign in
          </Link>
        </article>
      </section>
    );
  }

  const onSubmit = async (values: TwoFactorFormValues) => {
    clearError();
    setSubmitError(null);
    setInfoMessage(null);

    if(isExpired) {
      setSubmitError('This verification code has expired. Please request a new one.');
      return;
    }

    try {
      await verifyTwoFactor(challenge.challengeId, values.code);
      clearPendingTwoFactor();
      navigate(challenge.fromPath ?? '/dashboard', { replace: true });
    } catch (caughtError) {
      setSubmitError(
        getApiErrorMessage(caughtError, {
          defaultMessage: 'Unable to verify the code right now. Please try again.',
          rateLimitMessage: 'Too many verification attempts. Please wait a moment and try again.',
        }),
      );
    }
  };

  const handleResend = async () => {
    clearError();
    setSubmitError(null);
    setInfoMessage(null);

    try {
      const refreshedChallenge = await resendTwoFactor(challenge.challengeId);
      const nextChallenge: PendingTwoFactorChallenge = {
        ...refreshedChallenge,
        fromPath: challenge.fromPath,
      };

      savePendingTwoFactor(nextChallenge);
      setChallenge(nextChallenge);
      setInfoMessage('A new verification code has been sent to your email inbox.');
    } catch (caughtError) {
      setSubmitError(
        getApiErrorMessage(caughtError, {
          defaultMessage: 'Unable to resend the verification code right now. Please sign in again.',
          rateLimitMessage: 'Code resend is temporarily rate limited. Please wait a moment and try again.',
        }),
      );
    }
  };

  return (
    <section className="auth-layout">
      <article className="auth-callout">
        <p className="eyebrow">Email 2FA</p>
        <h1>Complete the sign-in.</h1>
        <p>
          We sent a one-time verification code to <strong>{challenge.destinationHint}</strong>. Enter it below to finish the login.
        </p>
        <ul className="auth-callout__list">
          <li>The code is single-use and expires at {new Date(challenge.expiresAt).toLocaleTimeString()}.</li>
          <li>Refreshing the page keeps the current challenge until you complete or restart the sign-in.</li>
          <li>You can request a fresh code if the email did not arrive or the code expired.</li>
        </ul>
      </article>

      <article className="auth-panel">
        <div className="auth-panel__header">
          <p className="eyebrow">Verification</p>
          <h2>Enter your code</h2>
          <p>Type the code from the latest email message to continue.</p>
        </div>

        <form className="form" noValidate onSubmit={handleSubmit(onSubmit)}>
          <label className="field">
            <span className="field__label">Verification code</span>
            <input
              autoComplete="one-time-code"
              inputMode="numeric"
              placeholder="123456"
              aria-invalid={errors.code ? 'true' : 'false'}
              {...register('code')}
            />
            {errors.code ? <span className="field__error">{errors.code.message}</span> : null}
          </label>
                    {isExpired ? <p className="form__warning">This code has expired. Use "Resend code" to get a new one.</p> : null}
          {infoMessage ? <p className="form__warning">{infoMessage}</p> : null}
          {submitError ?? error ? <p className="form__error">{submitError ?? error}</p> : null}
                    <button className="button button--block" type="submit" disabled={loading || isExpired}>
            {loading ? 'Verifying...' : 'Verify code'}
          </button>
          <button className="button button--ghost button--block" type="button" onClick={handleResend} disabled={loading}>
            Resend code
          </button>
        </form>

        <p className="auth-panel__footer">
          Need to start again?{' '}
          <Link
            to="/login"
            onClick={() => {
              clearPendingTwoFactor();
            }}
          >
            Return to sign in
          </Link>
          .
        </p>
      </article>
    </section>
  );
}