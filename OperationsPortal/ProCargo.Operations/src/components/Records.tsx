import { useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Checkbox,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  Grid,
  IconButton,
  MenuItem,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import VisibilityOutlined from '@mui/icons-material/VisibilityOutlined';
import { Controller, useForm } from 'react-hook-form';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { documentApi } from '../api/endpoints';
import type { DocumentEntityType } from '../api/types';
import { openFile, toApiError } from '../api/client';
import { applyApiErrors, ReasonDialog, useNotify } from './Forms';
import { EmptyState, QueryView } from './Layout';
import { StatusChip } from './PlateTag';
import { daysUntil, formatDateOnly, formatDateTime, fromLocalInput } from '../lib/format';
import { verificationStatus } from '../lib/statuses';

// ---------------- generic record editor ----------------

export interface FieldSpec {
  name: string;
  label: string;
  type?: 'text' | 'number' | 'date' | 'datetime' | 'select' | 'checkbox' | 'multiline';
  options?: { value: string | number; label: string }[];
  required?: boolean;
  width?: 6 | 12 | 4;
  helper?: string;
  disabled?: boolean;
}

/**
 * Small create/edit dialog for configuration records (master data, rate cards, rules). Required fields are checked
 * here; every other rule is enforced by the API, whose field errors are shown on the matching inputs.
 */
export function RecordDialog({
  title,
  fields,
  initial,
  onSubmit,
  onClose,
  submitLabel = 'Save',
}: {
  title: string;
  fields: FieldSpec[];
  initial: Record<string, unknown>;
  onSubmit: (values: Record<string, unknown>) => Promise<unknown>;
  onClose: () => void;
  submitLabel?: string;
}) {
  const [error, setError] = useState('');
  const { control, handleSubmit, setError: setFieldError, formState } = useForm<Record<string, unknown>>({ defaultValues: initial });

  const submit = handleSubmit(async (values) => {
    setError('');
    const missing = fields.filter((f) => f.required && (values[f.name] === '' || values[f.name] == null));
    if (missing.length) {
      missing.forEach((f) => setFieldError(f.name, { message: `Enter ${f.label.toLowerCase()}` }));
      return;
    }
    const normalized: Record<string, unknown> = {};
    for (const f of fields) {
      const value = values[f.name];
      if (f.type === 'number') normalized[f.name] = value === '' || value == null ? null : Number(value);
      else if (f.type === 'datetime') normalized[f.name] = value ? fromLocalInput(String(value)) : null;
      else if (f.type === 'checkbox') normalized[f.name] = !!value;
      else if (f.type === 'select') normalized[f.name] = value === '' ? null : value;
      else normalized[f.name] = value === '' ? null : value;
    }
    try {
      await onSubmit(normalized);
    } catch (e) {
      setError(applyApiErrors(e, setFieldError));
    }
  });

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <form onSubmit={submit} noValidate>
        <DialogTitle>{title}</DialogTitle>
        <DialogContent>
          {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
          <Grid container spacing={2} sx={{ mt: 0.5 }}>
            {fields.map((f) => (
              <Grid key={f.name} size={{ xs: 12, sm: f.width ?? 12 }}>
                <Controller
                  control={control}
                  name={f.name}
                  render={({ field, fieldState }) =>
                    f.type === 'checkbox' ? (
                      <FormControlLabel control={<Checkbox checked={!!field.value} onChange={(e) => field.onChange(e.target.checked)} />} label={f.label} />
                    ) : (
                      <TextField
                        {...field}
                        value={field.value ?? ''}
                        label={f.label}
                        disabled={f.disabled}
                        select={f.type === 'select'}
                        multiline={f.type === 'multiline'}
                        rows={f.type === 'multiline' ? 4 : undefined}
                        type={f.type === 'number' ? 'number' : f.type === 'date' ? 'date' : f.type === 'datetime' ? 'datetime-local' : 'text'}
                        error={!!fieldState.error}
                        helperText={fieldState.error?.message ?? f.helper}
                        slotProps={{ inputLabel: f.type === 'date' || f.type === 'datetime' ? { shrink: true } : undefined }}
                      >
                        {f.options?.map((o) => (
                          <MenuItem key={o.value} value={o.value}>
                            {o.label}
                          </MenuItem>
                        ))}
                      </TextField>
                    )
                  }
                />
              </Grid>
            ))}
          </Grid>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={onClose}>Cancel</Button>
          <Button type="submit" variant="contained" disabled={formState.isSubmitting}>
            {submitLabel}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}

// ---------------- KYC document review ----------------

/** Lists a partner's documents with view, verify and reject actions for staff who hold the approve permission. */
export function DocumentReview({ entity, entityId, canVerify }: { entity: DocumentEntityType; entityId: number; canVerify: boolean }) {
  const docs = useQuery({ queryKey: ['documents', entity, entityId], queryFn: () => documentApi.list(entity, entityId) });
  const queryClient = useQueryClient();
  const notify = useNotify();
  const [rejecting, setRejecting] = useState<number | null>(null);

  const decide = async (documentId: number, status: string, remarks?: string) => {
    await documentApi.verify(entity, entityId, documentId, status, remarks);
    notify(status === 'Verified' ? 'Document verified' : 'Document rejected');
    queryClient.invalidateQueries({ queryKey: ['documents', entity, entityId] });
  };

  return (
    <>
    <QueryView query={docs}>
      {(list) =>
        list.length === 0 ? (
          <EmptyState title="No documents uploaded" />
        ) : (
          <Stack spacing={1}>
            {list.map((d) => {
              const days = daysUntil(d.expiryDate);
              return (
                <Stack
                  key={d.documentId}
                  direction={{ xs: 'column', sm: 'row' }}
                  spacing={1}
                  sx={{ border: 1, borderColor: 'divider', borderRadius: 1.5, p: 1.5, justifyContent: 'space-between', alignItems: { sm: 'center' } }}
                >
                  <Box>
                    <Typography sx={{ fontWeight: 650 }}>{d.documentTypeName}</Typography>
                    <Typography variant="body2" color="text.secondary">
                      {d.documentNumber ?? 'No number'}
                      {d.expiryDate ? `, valid till ${formatDateOnly(d.expiryDate)}` : ''}
                      {days != null && days < 0 ? ' (expired)' : ''}, uploaded {formatDateTime(d.createdDateUtc)}
                    </Typography>
                    {d.remarks && <Typography variant="body2">{d.remarks}</Typography>}
                  </Box>
                  <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center' }}>
                    <StatusChip status={verificationStatus[d.verificationStatusId]} />
                    <IconButton
                      aria-label={`Open ${d.documentTypeName}`}
                      onClick={() => openFile(documentApi.fileUrl(entity, entityId, d.documentId)).catch((e) => notify(toApiError(e).message, 'error'))}
                    >
                      <VisibilityOutlined />
                    </IconButton>
                    {canVerify && d.verificationStatusId !== 3 && (
                      <Button size="small" variant="contained" onClick={() => decide(d.documentId, 'Verified').catch((e) => notify(toApiError(e).message, 'error'))}>
                        Verify
                      </Button>
                    )}
                    {canVerify && d.verificationStatusId !== 4 && (
                      <Button size="small" color="error" onClick={() => setRejecting(d.documentId)}>
                        Reject
                      </Button>
                    )}
                  </Stack>
                </Stack>
              );
            })}
          </Stack>
        )
      }
    </QueryView>
    <ReasonDialog
      open={rejecting != null}
      title="Reject this document?"
      description="The partner sees this reason and can upload a corrected document."
      confirmLabel="Reject document"
      destructive
      onClose={() => setRejecting(null)}
      onSubmit={(reason) => decide(rejecting!, 'Rejected', reason)}
    />
    </>
  );
}
