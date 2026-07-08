import { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { authApi } from '../services/api';
import { getApiErrorMessage } from '../utils/helpers';

type ConfirmationStatus = 'loading' | 'success' | 'error';

export default function ConfirmEmail() {
  const [searchParams] = useSearchParams();
  const [status, setStatus] = useState<ConfirmationStatus>('loading');
  const [message, setMessage] = useState('We are verifying your email confirmation link.');

  useEffect(() => {
    const userId = searchParams.get('userId')?.trim();
    const token = searchParams.get('token')?.trim();

    if (!userId || !token) {
      setStatus('error');
      setMessage('The confirmation link is incomplete. Request a new confirmation email and try again.');
      return;
    }

    let isMounted = true;

    const confirmEmail = async () => {
      try {
        await authApi.confirmEmail({ userId, token });
        if (!isMounted) {
          return;
        }

        setStatus('success');
        setMessage('Email confirmed successfully. You can now sign in and complete email 2FA.');
      } catch (error) {
        if (!isMounted) {
          return;
        }

        setStatus('error');
        setMessage(
          getApiErrorMessage(error, {
            defaultMessage: 'The confirmation link is invalid or expired. Request a new confirmation email and try again.',
          }),
        );
      }
    };

    void confirmEmail();

    return () => {
      isMounted = false;
    };
  }, [searchParams]);

  return (
    <section className="auth-layout">
      <article className="auth-callout">
        <p className="eyebrow">Account activation</p>
        <h1>Confirm your email address.</h1>
        <p>
          Email confirmation activates the account and unlocks the first sign-in flow with email-based two-factor verification.
        </p>
      </article>

      <article className="auth-panel">
        <div className="auth-panel__header">
          <p className="eyebrow">Email status</p>
          <h2>{status === 'loading' ? 'Confirming...' : status === 'success' ? 'Email confirmed' : 'Confirmation failed'}</h2>
          <p>{message}</p>
        </div>

        {status === 'loading' ? <p className="form__warning">Please wait while we validate the link.</p> : null}
        {status === 'error' ? <p className="form__error">{message}</p> : null}

        <div className="form">
          <Link className="button button--block" to="/login">
            {status === 'success' ? 'Continue to sign in' : 'Back to sign in'}
          </Link>
        </div>
      </article>
    </section>
  );
}