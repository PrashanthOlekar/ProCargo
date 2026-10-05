import { createContext, useCallback, useContext, useState, type ReactNode } from 'react';
import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  Snackbar,
  TextField,
} from '@mui/material';
import { useForm, Controller, type Control, type FieldValues, type Path, type UseFormSetError } from 'react-hook-form';
import { toApiError } from '../api/client';

// ---------------- toasts ----------------

interface Toast {
  message: string;
  severity: 'success' | 'error' | 'info';
}

const NotifyContext = createContext<(message: string, severity?: Toast['severity']) => void>(() => undefined);

export function NotifyProvider({ children }: { children: ReactNode }) {
  const [toast, setToast] = useState<Toast | null>(null);
  const notify = useCallback((message: string, severity: Toast['severity'] = 'success') => setToast({ message, severity }), []);
  return (
    <NotifyContext.Provider value={notify}>
      {children}
      <Snackbar
        open={!!toast}
        autoHideDuration={toast?.severity === 'error' ? 8000 : 4000}
        onClose={() => setToast(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        {toast ? (
          <Alert severity={toast.severity} variant="filled" onClose={() => setToast(null)}>
            {toast.message}
          </Alert>
        ) : undefined}
      </Snackbar>
    </NotifyContext.Provider>
  );
}

export const useNotify = () => useContext(NotifyContext);

/** Shows API field errors on the form fields they belong to; returns the message for anything left over. */
export function applyApiErrors<T extends FieldValues>(error: unknown, setError: UseFormSetError<T>): string {
  const apiError = toApiError(error);
  const unmatched: string[] = [];
  for (const [field, messages] of Object.entries(apiError.fieldErrors)) {
    if (field && field !== 'request') {
      setError(field as Path<T>, { type: 'server', message: messages[0] });
    } else {
      unmatched.push(...messages);
    }
  }
  return unmatched.length > 0 ? unmatched.join(' ') : Object.keys(apiError.fieldErrors).length > 0 ? '' : apiError.message;
}

// ---------------- form fields ----------------

interface FieldProps<T extends FieldValues> {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  control: Control<T, any, any>;
  name: Path<T>;
  label: string;
  type?: string;
  helperText?: string;
  autoComplete?: string;
  multiline?: boolean;
  rows?: number;
  select?: boolean;
  children?: ReactNode;
  disabled?: boolean;
  required?: boolean;
  inputMode?: 'numeric' | 'decimal' | 'tel' | 'email' | 'text';
  placeholder?: string;
  autoFocus?: boolean;
}

/** react-hook-form + MUI TextField with the validation message under the field. */
export function FormField<T extends FieldValues>({ control, name, label, helperText, inputMode, children, ...rest }: FieldProps<T>) {
  return (
    <Controller
      control={control}
      name={name}
      render={({ field, fieldState }) => (
        <TextField
          {...field}
          {...rest}
          value={field.value ?? ''}
          label={label}
          error={!!fieldState.error}
          helperText={fieldState.error?.message ?? helperText}
          slotProps={{ htmlInput: { inputMode }, inputLabel: rest.type === 'date' || rest.type === 'datetime-local' ? { shrink: true } : undefined }}
        >
          {children}
        </TextField>
      )}
    />
  );
}

// ---------------- dialogs ----------------

export function ConfirmDialog({
  open,
  title,
  body,
  confirmLabel,
  destructive,
  busy,
  onConfirm,
  onClose,
}: {
  open: boolean;
  title: string;
  body: ReactNode;
  confirmLabel: string;
  destructive?: boolean;
  busy?: boolean;
  onConfirm: () => void;
  onClose: () => void;
}) {
  return (
    <Dialog open={open} onClose={busy ? undefined : onClose} maxWidth="xs" fullWidth>
      <DialogTitle>{title}</DialogTitle>
      <DialogContent>
        <DialogContentText component="div">{body}</DialogContentText>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button onClick={onClose} disabled={busy}>
          Keep as is
        </Button>
        <Button variant="contained" color={destructive ? 'error' : 'primary'} onClick={onConfirm} disabled={busy}>
          {confirmLabel}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

/** Asks for a reason before an action that needs one (cancel, reject, report a problem). */
export function ReasonDialog({
  open,
  title,
  description,
  label = 'Reason',
  confirmLabel,
  destructive,
  onSubmit,
  onClose,
}: {
  open: boolean;
  title: string;
  description?: ReactNode;
  label?: string;
  confirmLabel: string;
  destructive?: boolean;
  onSubmit: (reason: string) => Promise<unknown>;
  onClose: () => void;
}) {
  const { control, handleSubmit, reset, setError, formState } = useForm<{ reason: string }>({ defaultValues: { reason: '' } });
  const [error, setFormError] = useState('');

  const close = () => {
    reset();
    setFormError('');
    onClose();
  };

  const submit = handleSubmit(async ({ reason }) => {
    if (reason.trim().length < 3) {
      setError('reason', { message: 'Please give a short reason' });
      return;
    }
    try {
      await onSubmit(reason.trim());
      close();
    } catch (e) {
      setFormError(applyApiErrors(e, setError));
    }
  });

  return (
    <Dialog open={open} onClose={formState.isSubmitting ? undefined : close} maxWidth="sm" fullWidth>
      <form onSubmit={submit} noValidate>
        <DialogTitle>{title}</DialogTitle>
        <DialogContent>
          {description && <DialogContentText sx={{ mb: 2 }}>{description}</DialogContentText>}
          {error && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {error}
            </Alert>
          )}
          <FormField control={control} name="reason" label={label} multiline rows={3} autoFocus />
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={close} disabled={formState.isSubmitting}>
            Go back
          </Button>
          <Button type="submit" variant="contained" color={destructive ? 'error' : 'primary'} disabled={formState.isSubmitting}>
            {confirmLabel}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
