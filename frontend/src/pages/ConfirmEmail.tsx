import { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { authApi } from '../services/api';
import { getApiErrorMessage } from '../utils/helpers';
import type { ResendConfirmationFormValues } from '../utils/authSchemas';
import { resendConfirmationSchema } from '../utils/authSchemas';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';

type ConfirmationStatus = 'loading' | 'success' | 'error';
type ResendStatus = 'idle' | 'loading' | 'success' | 'error';


export default function ConfirmEmail() {
  const [searchParams] = useSearchParams();
  const [status, setStatus] = useState<ConfirmationStatus>('loading');
  const [message, setMessage] = useState('We are verifying your email confirmation link.');
  const [resendStatus, setResendStatus] = useState<ResendStatus>('idle');
  const[resendError, setResendError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<ResendConfirmationFormValues>({
    resolver: zodResolver(resendConfirmationSchema),
    defaultValues: {
      email: '',
    },
  });

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

  const onResendSubmit = async (values: ResendConfirmationFormValues) => {
    setResendStatus('loading');
    setResendError(null);

    try{
      await authApi.resendConfirmation({ email: values.email });
      setResendStatus('success');
    } catch (error) {
      setResendStatus('error');
      setResendError(
        getApiErrorMessage(error, {
          defaultMessage: 'Unable to resend the confirmation email right now. Please try again.',
          rateLimitMessage: 'Too many requests. Please wait a moment and try again.',
        }),
      );
    }
  }

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

        {status === 'error' ? (
          resendStatus === 'success' ? (
            <p className="form__success">A new confirmation email has been sent. Please check your inbox.</p>
          ) : (
            <form className="form" noValidate onSubmit={handleSubmit(onResendSubmit)}>
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
              {resendError ? <p className="form__error">{resendError}</p> : null}
              <button type="submit" className="button button--block" disabled={resendStatus === 'loading'}>
                {resendStatus === 'loading' ? 'Resending...' : 'Resend confirmation email'}
              </button>
            </form>
          )
        ) : null}

        <div className="form">
          <Link className="button button--block" to="/login">
            {status === 'success' ? 'Continue to sign in' : 'Back to sign in'}
          </Link>
        </div>
      </article>
    </section>
  );
}