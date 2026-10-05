import { z } from 'zod';

/** Client-side mirrors of the API's FluentValidation rules. The API stays the authority. */

export const phone = z
  .string()
  .trim()
  .refine((v) => /^(\+91[\s-]?)?[6-9]\d{4}[\s-]?\d{5}$/.test(v) || /^\+[1-9]\d{7,14}$/.test(v.replace(/[\s-]/g, '')), {
    message: 'Enter a 10-digit mobile number',
  });

export const optionalPhone = z
  .string()
  .trim()
  .optional()
  .or(z.literal(''))
  .refine((v) => !v || /^(\+91[\s-]?)?[6-9]\d{4}[\s-]?\d{5}$/.test(v), { message: 'Enter a 10-digit mobile number' });

export const email = z.string().trim().min(1, 'Enter your e-mail').email('Enter a valid e-mail address').max(256);

export const password = z
  .string()
  .min(8, 'Use at least 8 characters')
  .max(128)
  .regex(/[A-Z]/, 'Add an uppercase letter')
  .regex(/[a-z]/, 'Add a lowercase letter')
  .regex(/[0-9]/, 'Add a number')
  .regex(/[^A-Za-z0-9]/, 'Add a symbol such as @ or #');

export const personName = z.string().trim().min(2, 'Enter the full name').max(150);

export const pincode = z.string().trim().regex(/^[1-9]\d{5}$/, 'Enter a 6-digit PIN code');

export const gst = z
  .string()
  .trim()
  .toUpperCase()
  .optional()
  .or(z.literal(''))
  .refine((v) => !v || /^\d{2}[A-Z]{5}\d{4}[A-Z][1-9A-Z]Z[0-9A-Z]$/.test(v), { message: 'Enter a valid 15-character GSTIN' });

export const vehicleNumber = z
  .string()
  .trim()
  .transform((v) => v.toUpperCase().replace(/[^A-Z0-9]/g, ''))
  .refine((v) => /^[A-Z]{2}\d{1,2}[A-Z]{0,3}\d{4}$/.test(v) || /^\d{2}BH\d{4}[A-Z]{1,2}$/.test(v), {
    message: 'Enter a registration number like KA 25 AB 1234',
  });

export const ifsc = z.string().trim().toUpperCase().regex(/^[A-Z]{4}0[A-Z0-9]{6}$/, 'Enter an 11-character IFSC');

export const accountNumber = z.string().trim().regex(/^\d{9,18}$/, 'Account numbers have 9 to 18 digits');

export const otp = z.string().trim().regex(/^\d{6}$/, 'Enter the 6-digit OTP');

/** Free text that must not carry markup. */
export const safeText = (max: number) =>
  z
    .string()
    .trim()
    .max(max, `Keep it under ${max} characters`)
    .refine((v) => !/[<>]/.test(v), { message: 'Remove < and > characters' });
