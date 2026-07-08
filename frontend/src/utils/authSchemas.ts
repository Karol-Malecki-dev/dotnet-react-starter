import { z } from 'zod';

export const loginSchema = z.object({
  email: z.email('Enter a valid email address.'),
  password: z.string().min(8, 'Password must be at least 8 characters long.'),
});

export const registerSchema = z.object({
  firstName: z.string().trim().min(1, 'First name is required.'),
  lastName: z.string().trim().min(1, 'Last name is required.'),
  email: z.email('Enter a valid email address.'),
  phoneNumber: z.string().trim().min(6, 'Phone number must be at least 6 characters long.'),
  address: z.string().trim().min(3, 'Address must be at least 3 characters long.'),
  password: z.string().min(8, 'Password must be at least 8 characters long.'),
});

export const twoFactorSchema = z.object({
  code: z
    .string()
    .trim()
    .regex(/^\d{4,10}$/, 'Enter the verification code exactly as it appears in the email.'),
});

export const forgotPasswordSchema = z.object({
  email: z.email('Enter a valid email address.'),
});

export const resetPasswordSchema = z
  .object({
    password: z.string().min(8, 'Password must be at least 8 characters long.'),
    confirmPassword: z.string().min(8, 'Confirm password must be at least 8 characters long.'),
  })
  .refine((data) => data.password === data.confirmPassword, {
    message: 'Passwords do not match.',
    path: ['confirmPassword'],
  });

export const resendConfirmationSchema = z.object({
  email: z.email('Enter a valid email address.'),
});

export type LoginFormValues = z.infer<typeof loginSchema>;
export type RegisterFormValues = z.infer<typeof registerSchema>;
export type TwoFactorFormValues = z.infer<typeof twoFactorSchema>;
export type ForgotPasswordFormValues = z.infer<typeof forgotPasswordSchema>;
export type ResetPasswordFormValues = z.infer<typeof resetPasswordSchema>;
export type ResendConfirmationFormValues = z.infer<typeof resendConfirmationSchema>;