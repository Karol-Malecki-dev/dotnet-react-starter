import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { authApi } from '../services/api';
import { ResetType } from '../types';
import type { ForgotPasswordFormValues } from '../utils/authSchemas';
import { forgotPasswordSchema } from '../utils/authSchemas';
import { getApiErrorMessage } from '../utils/helpers';

export default function ForgotPassword() {
  const [submitted, setSubmitted] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<ForgotPasswordFormValues>({
    resolver: zodResolver(forgotPasswordSchema),
    defaultValues: {
      email: '',
    },
  });

  const onSubmit = async (values: ForgotPasswordFormValues) => {
    setSubmitError(null);
    setLoading(true);

    try {
      await authApi.forgotPassword({ email: values.email, resetType: ResetType.Link });

      // The backend always answers with the same neutral message, whether or not the
      // account exists, to protect against account enumeration. The UI mirrors that
      // by showing the same success state for every submitted email.
      setSubmitted(true);
    } catch (error) {
      setSubmitError(
        getApiErrorMessage(error, {
          defaultMessage: 'Unable to send the reset email right now. Please try again.',
          rateLimitMessage: 'Too many requests. Please wait a moment and try again.',
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
        <h1>Reset your password.</h1>
        <p>Enter the email address linked to your account. If it exists, we will send a link to reset your password.</p>
        <ul className="auth-callout__list">
          <li>The response is always the same, so no one can tell whether an account exists.</li>
          <li>The reset link expires after a short time window for security.</li>
        </ul>
      </article>

      <article className="auth-panel">
        <div className="auth-panel__header">
          <p className="eyebrow">Forgot password</p>
          <h2>{submitted ? 'Check your inbox' : 'Send reset link'}</h2>
          <p>
            {submitted
              ? 'If an account exists for that address, a password reset link is on its way.'
              : 'We will email a secure, time-limited link to reset your password.'}
          </p>
        </div>

        {submitted ? (
          <div className="form">
            <p className="form__success">Reset email sent, if an account exists for that address. Check your inbox and spam folder.</p>
            <Link className="button button--block" to="/login">
              Back to sign in
            </Link>
          </div>
        ) : (
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
            {submitError ? <p className="form__error">{submitError}</p> : null}
            <button className="button button--block" type="submit" disabled={loading}>
              {loading ? 'Sending...' : 'Send reset link'}
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
