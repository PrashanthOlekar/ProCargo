import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  Checkbox,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  FormGroup,
  Grid,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { adminApi } from '../api/endpoints';
import type { AuditLog, Permission, Role, UserListItem } from '../api/types';
import { toApiError } from '../api/client';
import { P, useAuth } from '../auth/AuthContext';
import { DataTable } from '../components/DataTable';
import { FilterSelect, useUrlFilter } from '../components/Filters';
import { ConfirmDialog, useNotify } from '../components/Forms';
import { DetailList, PageHeader, QueryView, Section } from '../components/Layout';
import { RecordDialog } from '../components/Records';
import { formatDateTime, fromNow } from '../lib/format';
import { back } from './BookingPages';

// ---------------- users ----------------

export function UsersPage() {
  const navigate = useNavigate();
  const [kind, setKind] = useUrlFilter('kind');
  const [active, setActive] = useUrlFilter('active');
  const [creating, setCreating] = useState(false);
  const roles = useQuery({ queryKey: ['roles'], queryFn: adminApi.roles });
  const notify = useNotify();

  return (
    <>
      <PageHeader title="Users" subtitle="Staff accounts are created here. Customers, owners and drivers register themselves." actions={<Button variant="contained" onClick={() => setCreating(true)}>Add staff member</Button>} />
      <DataTable<UserListItem>
        queryKey={['users', kind, active]}
        fetcher={(q) => adminApi.users({ ...q, isInternal: kind === '' ? undefined : kind === 'staff', isActive: active === '' ? undefined : active === 'yes' })}
        rowKey={(u) => u.userId}
        onRowClick={(u) => navigate(`/users/${u.userId}`)}
        searchPlaceholder="Name, e-mail or phone"
        filters={
          <>
            <FilterSelect label="Account" value={kind} onChange={setKind} options={[{ value: 'staff', label: 'Staff' }, { value: 'external', label: 'Customers & partners' }]} />
            <FilterSelect label="State" value={active} onChange={setActive} options={[{ value: 'yes', label: 'Active' }, { value: 'no', label: 'Deactivated' }]} width={160} />
          </>
        }
        empty={{ title: 'No users match' }}
        columns={[
          {
            header: 'User',
            primary: true,
            render: (u) => (
              <>
                <Typography sx={{ fontWeight: 600 }}>{u.fullName}</Typography>
                <Typography variant="body2" color="text.secondary">{u.email}</Typography>
              </>
            ),
          },
          { header: 'Phone', render: (u) => u.phoneNumber },
          { header: 'Roles', primary: true, render: (u) => u.roles || '—' },
          { header: 'Last sign-in', render: (u) => (u.lastLoginDateUtc ? fromNow(u.lastLoginDateUtc) : 'Never') },
          {
            header: 'State',
            primary: true,
            render: (u) => (
              <Stack direction="row" spacing={0.5}>
                {!u.isActive && <Chip size="small" label="Deactivated" />}
                {u.isLocked && <Chip size="small" color="warning" label="Locked" />}
                {u.isActive && !u.isLocked && <Chip size="small" color="success" variant="outlined" label="Active" />}
              </Stack>
            ),
          },
        ]}
      />
      {creating && (
        <RecordDialog
          title="Add staff member"
          submitLabel="Create and send invite"
          fields={[
            { name: 'fullName', label: 'Full name', required: true },
            { name: 'email', label: 'Work e-mail', required: true, width: 6 },
            { name: 'phoneNumber', label: 'Mobile number', required: true, width: 6 },
            {
              name: 'roleId',
              label: 'Role',
              type: 'select',
              required: true,
              options: (roles.data ?? []).filter((r) => r.isInternal && r.isActive).map((r) => ({ value: r.roleId, label: r.name })),
              helper: 'More roles can be added after the account is created.',
            },
          ]}
          initial={{ fullName: '', email: '', phoneNumber: '', roleId: '' }}
          onClose={() => setCreating(false)}
          onSubmit={async (v) => {
            const created = await adminApi.createUser({ fullName: v.fullName, email: v.email, phoneNumber: v.phoneNumber, roleIds: [Number(v.roleId)] });
            notify('Account created. A link to set the password has been e-mailed.');
            setCreating(false);
            navigate(`/users/${created.id}`);
          }}
        />
      )}
    </>
  );
}

export function UserDetailPage() {
  const id = Number(useParams().id);
  const { user: me } = useAuth();
  const query = useQuery({ queryKey: ['user', id], queryFn: () => adminApi.user(id) });
  const roles = useQuery({ queryKey: ['roles'], queryFn: adminApi.roles });
  const queryClient = useQueryClient();
  const notify = useNotify();
  const [selected, setSelected] = useState<number[]>([]);
  const [editing, setEditing] = useState(false);
  const [confirm, setConfirm] = useState<'deactivate' | null>(null);

  useEffect(() => {
    if (query.data) setSelected(query.data.roleIds);
  }, [query.data]);

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['user', id] });
    queryClient.invalidateQueries({ queryKey: ['users'] });
  };
  const run = async (fn: () => Promise<unknown>, done: string) => {
    try {
      await fn();
      notify(done);
      refresh();
    } catch (e) {
      notify(toApiError(e).message, 'error');
    }
  };

  return (
    <QueryView query={query}>
      {({ user: u, roleIds }) => {
        const self = u.userId === me?.userId;
        const locked = !!u.lockoutEndUtc && new Date(u.lockoutEndUtc) > new Date();
        const assignable = (roles.data ?? []).filter((r) => r.isInternal === u.isInternal);
        const changed = selected.length !== roleIds.length || selected.some((r) => !roleIds.includes(r));
        return (
          <>
            <PageHeader
              back={back('/users', 'Users')}
              title={u.fullName}
              subtitle={u.email}
              actions={
                <>
                  {locked && <Button variant="outlined" onClick={() => run(() => adminApi.userAction(id, 'unlock'), 'Account unlocked')}>Unlock</Button>}
                  <Button variant="outlined" onClick={() => setEditing(true)}>Edit details</Button>
                  {u.isActive
                    ? !self && <Button color="error" onClick={() => setConfirm('deactivate')}>Deactivate</Button>
                    : <Button variant="contained" onClick={() => run(() => adminApi.userAction(id, 'activate'), 'Account activated')}>Activate</Button>}
                </>
              }
            />
            {!u.isActive && <Alert severity="warning" sx={{ mb: 2 }}>This account is deactivated and cannot sign in.</Alert>}
            {locked && <Alert severity="warning" sx={{ mb: 2 }}>Locked after repeated failed sign-ins until {formatDateTime(u.lockoutEndUtc)}.</Alert>}
            <Grid container spacing={3}>
              <Grid size={{ xs: 12, md: 5 }}>
                <Section title="Account">
                  <DetailList
                    columns={1}
                    items={[
                      ['Type', u.isInternal ? 'Staff' : 'Customer or partner'],
                      ['Phone', u.phoneNumber],
                      ['Created', formatDateTime(u.createdDateUtc)],
                      ['Last sign-in', formatDateTime(u.lastLoginDateUtc)],
                      ['Password', u.mustChangePassword ? 'Must be changed at next sign-in' : 'Set'],
                    ]}
                  />
                </Section>
              </Grid>
              <Grid size={{ xs: 12, md: 7 }}>
                <Section
                  title="Roles"
                  action={
                    <Button variant="contained" disabled={self || !changed || selected.length === 0} onClick={() => run(() => adminApi.setUserRoles(id, selected), 'Roles saved. They apply from the next sign-in or token refresh.')}>
                      Save roles
                    </Button>
                  }
                >
                  {self && <Alert severity="info" sx={{ mb: 2 }}>You cannot change your own roles. Ask another administrator.</Alert>}
                  <FormGroup>
                    {assignable.map((r) => (
                      <FormControlLabel
                        key={r.roleId}
                        control={
                          <Checkbox
                            checked={selected.includes(r.roleId)}
                            disabled={self || !r.isActive}
                            onChange={(e) => setSelected(e.target.checked ? [...selected, r.roleId] : selected.filter((x) => x !== r.roleId))}
                          />
                        }
                        label={
                          <Box>
                            <Typography sx={{ fontWeight: 600 }}>{r.name}</Typography>
                            {r.description && <Typography variant="body2" color="text.secondary">{r.description}</Typography>}
                          </Box>
                        }
                        sx={{ alignItems: 'flex-start', mb: 1, '& .MuiCheckbox-root': { pt: 0.5 } }}
                      />
                    ))}
                  </FormGroup>
                  {selected.length === 0 && <Typography color="error" variant="body2">Every account needs at least one role.</Typography>}
                </Section>
              </Grid>
            </Grid>
            {editing && (
              <RecordDialog
                title="Edit details"
                fields={[
                  { name: 'fullName', label: 'Full name', required: true },
                  { name: 'phoneNumber', label: 'Mobile number', required: true },
                ]}
                initial={{ fullName: u.fullName, phoneNumber: u.phoneNumber }}
                onClose={() => setEditing(false)}
                onSubmit={async (v) => {
                  await adminApi.updateUser(id, { ...v, rowVersion: u.rowVersion });
                  notify('Details saved');
                  setEditing(false);
                  refresh();
                }}
              />
            )}
            <ConfirmDialog
              open={confirm === 'deactivate'}
              title={`Deactivate ${u.fullName}?`}
              body="They are signed out everywhere and cannot sign in until the account is activated again."
              confirmLabel="Deactivate"
              destructive
              onClose={() => setConfirm(null)}
              onConfirm={() => {
                setConfirm(null);
                run(() => adminApi.userAction(id, 'deactivate'), 'Account deactivated');
              }}
            />
          </>
        );
      }}
    </QueryView>
  );
}

// ---------------- roles & permissions ----------------

export function RolesPage() {
  const query = useQuery({ queryKey: ['roles'], queryFn: adminApi.roles });
  const [openRole, setOpenRole] = useState<Role | null>(null);
  const [creating, setCreating] = useState(false);
  const notify = useNotify();
  const queryClient = useQueryClient();

  return (
    <>
      <PageHeader
        title="Roles & permissions"
        subtitle="A role is a named set of permissions. System roles can be adjusted but not deleted."
        actions={<Button variant="contained" onClick={() => setCreating(true)}>New role</Button>}
      />
      <QueryView query={query}>
        {(roles) => (
          <Grid container spacing={2}>
            {roles.map((r) => (
              <Grid key={r.roleId} size={{ xs: 12, sm: 6, lg: 4 }}>
                <Box
                  component="button"
                  type="button"
                  onClick={() => setOpenRole(r)}
                  sx={{
                    all: 'unset',
                    boxSizing: 'border-box',
                    display: 'block',
                    width: '100%',
                    height: '100%',
                    cursor: 'pointer',
                    p: 2.5,
                    border: 1,
                    borderColor: 'divider',
                    borderRadius: 2,
                    bgcolor: 'background.paper',
                    opacity: r.isActive ? 1 : 0.6,
                    '&:hover': { borderColor: 'primary.main' },
                    '&:focus-visible': { outline: '2px solid', outlineColor: 'primary.main', outlineOffset: 2 },
                  }}
                >
                  <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'baseline', mb: 0.5 }}>
                    <Typography variant="h6">{r.name}</Typography>
                    <Typography variant="body2" color="text.secondary">{r.isInternal ? 'Staff' : 'External'}</Typography>
                  </Stack>
                  <Typography variant="body2" color="text.secondary" sx={{ minHeight: 40 }}>{r.description ?? 'No description'}</Typography>
                  <Typography variant="body2" sx={{ mt: 1 }}>
                    {r.permissionCount} permissions, {r.userCount} {r.userCount === 1 ? 'user' : 'users'}
                    {r.isSystem ? ', system role' : ''}
                    {r.isActive ? '' : ', inactive'}
                  </Typography>
                </Box>
              </Grid>
            ))}
          </Grid>
        )}
      </QueryView>
      {openRole && <RoleEditor role={openRole} onClose={() => setOpenRole(null)} />}
      {creating && (
        <RecordDialog
          title="New role"
          fields={[
            { name: 'name', label: 'Name', required: true },
            { name: 'description', label: 'What this role is for', type: 'multiline' },
            { name: 'isInternal', label: 'Staff role (uses the Operations portal)', type: 'checkbox' },
          ]}
          initial={{ name: '', description: '', isInternal: true }}
          onClose={() => setCreating(false)}
          onSubmit={async (v) => {
            await adminApi.createRole(v);
            notify('Role created. Choose its permissions next.');
            setCreating(false);
            queryClient.invalidateQueries({ queryKey: ['roles'] });
          }}
        />
      )}
    </>
  );
}

function RoleEditor({ role, onClose }: { role: Role; onClose: () => void }) {
  const details = useQuery({ queryKey: ['role', role.roleId], queryFn: () => adminApi.role(role.roleId) });
  const all = useQuery({ queryKey: ['permissions'], queryFn: adminApi.permissions });
  const { can } = useAuth();
  const queryClient = useQueryClient();
  const notify = useNotify();
  const [selected, setSelected] = useState<number[] | null>(null);
  const [description, setDescription] = useState(role.description ?? '');
  const [isActive, setIsActive] = useState(role.isActive);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    if (details.data && selected === null) setSelected(details.data.permissions.map((p) => p.permissionId));
  }, [details.data, selected]);

  // Only permissions of the role's own kind can be granted: staff permissions to staff roles, portal permissions to external roles.
  const modules = useMemo(() => {
    const grouped = new Map<string, Permission[]>();
    for (const p of all.data ?? []) {
      if (p.isInternal !== role.isInternal) continue;
      grouped.set(p.module, [...(grouped.get(p.module) ?? []), p]);
    }
    return Array.from(grouped.entries());
  }, [all.data, role.isInternal]);

  const toggle = (ids: number[], on: boolean) =>
    setSelected((s) => (on ? Array.from(new Set([...(s ?? []), ...ids])) : (s ?? []).filter((x) => !ids.includes(x))));

  const save = async () => {
    setBusy(true);
    setError('');
    try {
      await adminApi.updateRole(role.roleId, { description: description.trim() || null, isActive });
      await adminApi.setRolePermissions(role.roleId, selected ?? []);
      notify('Role saved. Users get the change at their next sign-in or token refresh.');
      queryClient.invalidateQueries({ queryKey: ['roles'] });
      queryClient.invalidateQueries({ queryKey: ['role', role.roleId] });
      onClose();
    } catch (e) {
      setError(toApiError(e).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open onClose={busy ? undefined : onClose} maxWidth="md" fullWidth>
      <DialogTitle>{role.name}</DialogTitle>
      <DialogContent dividers>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        {!can(P.ManageRoles) && <Alert severity="info" sx={{ mb: 2 }}>You can view this role but not change it.</Alert>}
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ mb: 3 }}>
          <TextField label="Description" value={description} onChange={(e) => setDescription(e.target.value)} fullWidth />
          <FormControlLabel control={<Checkbox checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />} label="Active" />
        </Stack>
        {details.isLoading || all.isLoading || selected === null ? (
          <Typography color="text.secondary">Loading permissions</Typography>
        ) : (
          <Grid container spacing={3}>
            {modules.map(([module, perms]) => {
              const ids = perms.map((p) => p.permissionId);
              const on = ids.filter((x) => selected.includes(x)).length;
              return (
                <Grid key={module} size={{ xs: 12, sm: 6 }}>
                  <FormControlLabel
                    control={<Checkbox checked={on === ids.length} indeterminate={on > 0 && on < ids.length} onChange={(e) => toggle(ids, e.target.checked)} />}
                    label={<Typography sx={{ fontWeight: 700 }}>{module}</Typography>}
                  />
                  <FormGroup sx={{ pl: 3.5 }}>
                    {perms.map((p) => (
                      <FormControlLabel
                        key={p.permissionId}
                        control={<Checkbox size="small" checked={selected.includes(p.permissionId)} onChange={(e) => toggle([p.permissionId], e.target.checked)} />}
                        label={<Typography variant="body2">{p.name}</Typography>}
                      />
                    ))}
                  </FormGroup>
                </Grid>
              );
            })}
          </Grid>
        )}
      </DialogContent>
      <DialogActions sx={{ px: 3, py: 2, justifyContent: 'space-between' }}>
        <Box>
          {!role.isSystem && role.userCount === 0 && (
            <Button color="error" onClick={() => setDeleting(true)} disabled={busy}>Delete role</Button>
          )}
        </Box>
        <Box>
          <Button onClick={onClose} disabled={busy}>Cancel</Button>
          <Button variant="contained" onClick={save} disabled={busy || !can(P.ManageRoles)} sx={{ ml: 1 }}>Save role</Button>
        </Box>
      </DialogActions>
      <ConfirmDialog
        open={deleting}
        title={`Delete ${role.name}?`}
        body="Nobody holds this role. Deleting it cannot be undone."
        confirmLabel="Delete"
        destructive
        busy={busy}
        onClose={() => setDeleting(false)}
        onConfirm={async () => {
          setBusy(true);
          try {
            await adminApi.deleteRole(role.roleId);
            notify('Role deleted');
            queryClient.invalidateQueries({ queryKey: ['roles'] });
            onClose();
          } catch (e) {
            setError(toApiError(e).message);
            setDeleting(false);
          } finally {
            setBusy(false);
          }
        }}
      />
    </Dialog>
  );
}

// ---------------- audit log ----------------

const entityTypes = ['Booking', 'Quotation', 'Trip', 'Invoice', 'Payment', 'Settlement', 'User', 'Role', 'Owner', 'Driver', 'Vehicle', 'Customer', 'PricingRule', 'SystemSetting', 'SupportTicket', 'Complaint'];

export function AuditLogPage() {
  const [entityType, setEntityType] = useUrlFilter('entity');
  const [entityId, setEntityId] = useUrlFilter('id');
  const [open, setOpen] = useState<AuditLog | null>(null);

  return (
    <>
      <PageHeader title="Audit log" subtitle="Every change made through the platform, newest first. Secrets are masked before they are recorded." />
      <DataTable<AuditLog>
        queryKey={['audit', entityType, entityId]}
        fetcher={(q) => adminApi.auditLogs({ ...q, entityType: entityType || undefined, entityId: entityId || undefined })}
        rowKey={(a) => a.auditLogId}
        onRowClick={setOpen}
        searchPlaceholder="Action or user"
        filters={
          <>
            <FilterSelect label="Record type" value={entityType} onChange={setEntityType} options={entityTypes.map((e) => ({ value: e, label: e }))} />
            <TextField label="Record id" value={entityId} onChange={(e) => setEntityId(e.target.value.replace(/\D/g, ''))} sx={{ width: { xs: '100%', sm: 140 } }} />
          </>
        }
        empty={{ title: 'No audit entries match' }}
        columns={[
          { header: 'When', primary: true, render: (a) => formatDateTime(a.createdDateUtc) },
          { header: 'Who', primary: true, render: (a) => a.userName ?? 'System' },
          { header: 'Action', primary: true, render: (a) => <Typography sx={{ fontWeight: 600 }}>{a.action}</Typography> },
          { header: 'Record', render: (a) => `${a.entityType}${a.entityId ? ` #${a.entityId}` : ''}` },
          { header: 'IP address', render: (a) => a.ipAddress ?? '—' },
        ]}
      />
      {open && (
        <Dialog open onClose={() => setOpen(null)} maxWidth="md" fullWidth>
          <DialogTitle>{open.action}</DialogTitle>
          <DialogContent dividers>
            <DetailList
              items={[
                ['When', formatDateTime(open.createdDateUtc)],
                ['Who', open.userName ?? 'System'],
                ['Record', `${open.entityType}${open.entityId ? ` #${open.entityId}` : ''}`],
                ['IP address', open.ipAddress ?? '—'],
                ['Trace id', open.traceId ?? '—'],
              ]}
            />
            <Grid container spacing={2} sx={{ mt: 1 }}>
              <Grid size={{ xs: 12, md: 6 }}>
                <Typography variant="body2" sx={{ fontWeight: 700, mb: 0.5 }}>Before</Typography>
                <JsonBlock value={open.oldValue} />
              </Grid>
              <Grid size={{ xs: 12, md: 6 }}>
                <Typography variant="body2" sx={{ fontWeight: 700, mb: 0.5 }}>After</Typography>
                <JsonBlock value={open.newValue} />
              </Grid>
            </Grid>
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setOpen(null)}>Close</Button>
          </DialogActions>
        </Dialog>
      )}
    </>
  );
}

function JsonBlock({ value }: { value: string | null }) {
  let text = value ?? '';
  try {
    if (value) text = JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    // stored as plain text
  }
  return (
    <Box component="pre" sx={{ m: 0, p: 1.5, bgcolor: 'action.hover', borderRadius: 1, fontSize: '0.8rem', overflow: 'auto', maxHeight: 360, whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>
      {text || '(nothing)'}
    </Box>
  );
}
