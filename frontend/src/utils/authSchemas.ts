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

export type LoginFormValues = z.infer<typeof loginSchema>;
export type RegisterFormValues = z.infer<typeof registerSchema>;
export type TwoFactorFormValues = z.infer<typeof twoFactorSchema>;