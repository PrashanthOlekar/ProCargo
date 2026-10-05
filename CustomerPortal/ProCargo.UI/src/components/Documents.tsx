import { useRef, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Grid,
  IconButton,
  MenuItem,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import DeleteOutline from '@mui/icons-material/DeleteOutline';
import VisibilityOutlined from '@mui/icons-material/VisibilityOutlined';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { documentApi, referenceApi } from '../api/endpoints';
import type { DocumentEntityType } from '../api/types';
import { openFile, toApiError } from '../api/client';
import { ConfirmDialog, useNotify } from './Forms';
import { EmptyState, QueryView } from './Layout';
import { StatusChip } from './PlateTag';
import { daysUntil, fileSize, formatDateOnly } from '../lib/format';
import { verificationStatus } from '../lib/statuses';

const appliesTo: Record<DocumentEntityType, string> = { Customer: 'Customer', Owner: 'Owner', Driver: 'Driver', Vehicle: 'Vehicle' };
const ACCEPT = '.pdf,.jpg,.jpeg,.png,.webp';
const MAX_BYTES = 10 * 1024 * 1024;

/**
 * KYC / compliance documents of one record. Uploads are checked here for size and type for a quick answer;
 * the API re-checks size, extension, MIME type and file signature before storing anything.
 */
export function DocumentsPanel({ entity, entityId, readOnly }: { entity: DocumentEntityType; entityId: number; readOnly?: boolean }) {
  const docs = useQuery({ queryKey: ['documents', entity, entityId], queryFn: () => documentApi.list(entity, entityId) });
  const types = useQuery({ queryKey: ['documentTypes', entity], queryFn: () => referenceApi.documentTypes(appliesTo[entity]), staleTime: 600_000 });
  const [uploadOpen, setUploadOpen] = useState(false);
  const [deleting, setDeleting] = useState<number | null>(null);
  const queryClient = useQueryClient();
  const notify = useNotify();

  const missing = (types.data ?? []).filter((t) => t.isMandatory && !(docs.data ?? []).some((d) => d.documentTypeId === t.documentTypeId && d.verificationStatusId !== 4));

  return (
    <Box>
      {!readOnly && missing.length > 0 && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          Still needed for verification: {missing.map((m) => m.name).join(', ')}.
        </Alert>
      )}
      <QueryView query={docs}>
        {(list) =>
          list.length === 0 ? (
            <EmptyState title="No documents uploaded">PDF or a clear photo, up to 10 MB each.</EmptyState>
          ) : (
            <Stack spacing={1}>
              {list.map((d) => {
                const days = daysUntil(d.expiryDate);
                return (
                  <Stack
                    key={d.documentId}
                    direction={{ xs: 'column', sm: 'row' }}
                    spacing={1}
                    sx={{ border: 1, borderColor: 'divider', borderRadius: 1.5, p: 1.5, alignItems: { sm: 'center' }, justifyContent: 'space-between' }}
                  >
                    <Box>
                      <Typography sx={{ fontWeight: 650 }}>{d.documentTypeName}</Typography>
                      <Typography variant="body2" color="text.secondary">
                        {d.documentNumber ? `${d.documentNumber}, ` : ''}
                        {d.expiryDate ? `valid till ${formatDateOnly(d.expiryDate)}` : d.originalFileName}
                        {days != null && days <= 30 ? (days < 0 ? ' (expired)' : ` (expires in ${days} days)`) : ''}
                      </Typography>
                      {d.remarks && d.verificationStatusId === 4 && (
                        <Typography variant="body2" color="error">
                          {d.remarks}
                        </Typography>
                      )}
                    </Box>
                    <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center' }}>
                      <StatusChip status={verificationStatus[d.verificationStatusId]} />
                      <IconButton
                        aria-label={`View ${d.documentTypeName}`}
                        onClick={() => openFile(documentApi.fileUrl(entity, entityId, d.documentId)).catch((e) => notify(toApiError(e).message, 'error'))}
                      >
                        <VisibilityOutlined />
                      </IconButton>
                      {!readOnly && d.verificationStatusId !== 3 && (
                        <IconButton aria-label={`Delete ${d.documentTypeName}`} onClick={() => setDeleting(d.documentId)}>
                          <DeleteOutline />
                        </IconButton>
                      )}
                    </Stack>
                  </Stack>
                );
              })}
            </Stack>
          )
        }
      </QueryView>
      {!readOnly && (
        <Button variant="outlined" sx={{ mt: 2 }} onClick={() => setUploadOpen(true)}>
          Upload a document
        </Button>
      )}
      {uploadOpen && (
        <UploadDialog
          entity={entity}
          entityId={entityId}
          types={types.data ?? []}
          onClose={() => setUploadOpen(false)}
          onUploaded={() => {
            setUploadOpen(false);
            queryClient.invalidateQueries({ queryKey: ['documents', entity, entityId] });
          }}
        />
      )}
      <ConfirmDialog
        open={deleting != null}
        title="Delete this document?"
        body="You can upload a replacement afterwards."
        confirmLabel="Delete"
        destructive
        onClose={() => setDeleting(null)}
        onConfirm={async () => {
          try {
            await documentApi.remove(entity, entityId, deleting!);
            notify('Document deleted');
            queryClient.invalidateQueries({ queryKey: ['documents', entity, entityId] });
          } catch (e) {
            notify(toApiError(e).message, 'error');
          } finally {
            setDeleting(null);
          }
        }}
      />
    </Box>
  );
}

function UploadDialog({
  entity,
  entityId,
  types,
  onClose,
  onUploaded,
}: {
  entity: DocumentEntityType;
  entityId: number;
  types: { documentTypeId: number; name: string; requiresExpiry: boolean }[];
  onClose: () => void;
  onUploaded: () => void;
}) {
  const [typeId, setTypeId] = useState<number>(types[0]?.documentTypeId ?? 0);
  const [number, setNumber] = useState('');
  const [expiry, setExpiry] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const input = useRef<HTMLInputElement>(null);
  const notify = useNotify();
  const type = types.find((t) => t.documentTypeId === typeId);

  const submit = async () => {
    setError('');
    if (!file) return setError('Choose the file to upload.');
    if (file.size > MAX_BYTES) return setError('The file is larger than 10 MB. Upload a smaller scan or photo.');
    if (!/\.(pdf|jpe?g|png|webp)$/i.test(file.name)) return setError('Upload a PDF, JPG, PNG or WebP file.');
    if (type?.requiresExpiry && !expiry) return setError('Enter the expiry date printed on the document.');

    const form = new FormData();
    form.append('documentTypeId', String(typeId));
    if (number) form.append('documentNumber', number.trim());
    if (expiry) form.append('expiryDate', expiry);
    form.append('file', file);

    setBusy(true);
    try {
      await documentApi.upload(entity, entityId, form);
      notify('Document uploaded. We will verify it shortly.');
      onUploaded();
    } catch (e) {
      setError(toApiError(e).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open onClose={busy ? undefined : onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Upload a document</DialogTitle>
      <DialogContent>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        <Grid container spacing={2} sx={{ mt: 0.5 }}>
          <Grid size={12}>
            <TextField select label="Document" value={typeId} onChange={(e) => setTypeId(Number(e.target.value))}>
              {types.map((t) => (
                <MenuItem key={t.documentTypeId} value={t.documentTypeId}>
                  {t.name}
                </MenuItem>
              ))}
            </TextField>
          </Grid>
          <Grid size={{ xs: 12, sm: 6 }}>
            <TextField label="Document number (optional)" value={number} onChange={(e) => setNumber(e.target.value)} />
          </Grid>
          <Grid size={{ xs: 12, sm: 6 }}>
            <TextField
              label={type?.requiresExpiry ? 'Valid till' : 'Valid till (optional)'}
              type="date"
              value={expiry}
              onChange={(e) => setExpiry(e.target.value)}
              slotProps={{ inputLabel: { shrink: true } }}
            />
          </Grid>
          <Grid size={12}>
            <input ref={input} type="file" accept={ACCEPT} hidden onChange={(e) => setFile(e.target.files?.[0] ?? null)} />
            <Button variant="outlined" onClick={() => input.current?.click()}>
              {file ? 'Choose a different file' : 'Choose file'}
            </Button>
            {file && (
              <Typography variant="body2" sx={{ mt: 1 }}>
                {file.name} ({fileSize(file.size)})
              </Typography>
            )}
          </Grid>
        </Grid>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button onClick={onClose} disabled={busy}>
          Cancel
        </Button>
        <Button variant="contained" onClick={submit} disabled={busy}>
          {busy ? 'Uploading…' : 'Upload'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
