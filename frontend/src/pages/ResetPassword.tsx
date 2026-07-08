import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { authApi } from '../services/api';
import { ResetType } from '../types';
import type { ResetPasswordFormValues } from '../utils/authSchemas';
import { resetPasswordSchema } from '../utils/authSchemas';
import { getApiErrorMessage } from '../utils/helpers';

export default function ResetPassword() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();

  // Both email and token arrive as query params from the link sent by /auth/forgot-password.
  const email = searchParams.get('email')?.trim() ?? '';
  const token = searchParams.get('token')?.trim() ?? '';
  const linkIsIncomplete = !email || !token;

  const [submitError, setSubmitError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<ResetPasswordFormValues>({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: {
      password: '',
      confirmPassword: '',
    },
  });

  const onSubmit = async (values: ResetPasswordFormValues) => {
    setSubmitError(null);
    setLoading(true);

    try {
      await authApi.resetPassword({
        email,
        token,
        resetType: ResetType.Link,
        newPassword: values.password,
      });

      navigate('/login', {
        replace: true,
        state: { reason: 'password-reset' },
      });
    } catch (error) {
      setSubmitError(
        getApiErrorMessage(error, {
          defaultMessage: 'This reset link is invalid or has expired. Request a new one and try again.',
          rateLimitMessage: 'Too many attempts. Please wait a moment and try again.',
        }),
      );
    } finally {
      setLoading(false);
    }
  };

  return (
    <section className="auth-layout">
      <article className="auth-callout">
        <p className="eyebrow">Account recovery</p>
        <h1>Choose a new password.</h1>
        <p>Set a new password for your account. You will need to sign in again afterwards.</p>
      </article>

      <article className="auth-panel">
        <div className="auth-panel__header">
          <p className="eyebrow">Reset password</p>
          <h2>{linkIsIncomplete ? 'Invalid reset link' : 'New password'}</h2>
          <p>
            {linkIsIncomplete
              ? 'This reset link is missing required information. Request a new password reset email and try again.'
              : `Set a new password for ${email}.`}
          </p>
        </div>

        {linkIsIncomplete ? (
          <div className="form">
            <Link className="button button--block" to="/forgot-password">
              Request a new reset link
            </Link>
          </div>
        ) : (
          <form className="form" noValidate onSubmit={handleSubmit(onSubmit)}>
            <label className="field">
              <span className="field__label">New password</span>
              <input
                type="password"
                autoComplete="new-password"
                placeholder="Enter a new password"
                aria-invalid={errors.password ? 'true' : 'false'}
                {...register('password')}
              />
              {errors.password ? <span className="field__error">{errors.password.message}</span> : null}
            </label>
            <label className="field">
              <span className="field__label">Confirm new password</span>
              <input
                type="password"
                autoComplete="new-password"
                placeholder="Repeat the new password"
                aria-invalid={errors.confirmPassword ? 'true' : 'false'}
                {...register('confirmPassword')}
              />
              {errors.confirmPassword ? <span className="field__error">{errors.confirmPassword.message}</span> : null}
            </label>
            {submitError ? <p className="form__error">{submitError}</p> : null}
            <button className="button button--block" type="submit" disabled={loading}>
              {loading ? 'Saving...' : 'Set new password'}
            </button>
          </form>
        )}

        <p className="auth-panel__footer">
          Remembered your password? <Link to="/login">Sign in</Link>.
        </p>
      </article>
    </section>
  );
}
