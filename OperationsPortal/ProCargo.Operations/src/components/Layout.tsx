import type { ReactNode } from 'react';
import { Alert, AlertTitle, Box, Button, Card, CardContent, CircularProgress, Stack, Typography } from '@mui/material';
import { toApiError } from '../api/client';

export function PageHeader({ title, subtitle, actions, back }: { title: ReactNode; subtitle?: ReactNode; actions?: ReactNode; back?: ReactNode }) {
  return (
    <Box sx={{ mb: 3 }}>
      {back}
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ alignItems: { sm: 'flex-end' }, justifyContent: 'space-between' }}>
        <Box>
          <Typography variant="h3" component="h1">
            {title}
          </Typography>
          {subtitle && (
            <Typography color="text.secondary" sx={{ mt: 0.5 }}>
              {subtitle}
            </Typography>
          )}
        </Box>
        {actions && (
          <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap', gap: 1 }}>
            {actions}
          </Stack>
        )}
      </Stack>
    </Box>
  );
}

export function Section({ title, action, children, dense }: { title?: ReactNode; action?: ReactNode; children: ReactNode; dense?: boolean }) {
  return (
    <Card sx={{ mb: 2.5 }}>
      <CardContent sx={{ p: dense ? 2 : 3, '&:last-child': { pb: dense ? 2 : 3 } }}>
        {(title || action) && (
          <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'center', mb: 2, gap: 1 }}>
            {title && (
              <Typography variant="h5" component="h2">
                {title}
              </Typography>
            )}
            {action}
          </Stack>
        )}
        {children}
      </CardContent>
    </Card>
  );
}

/** Label/value pairs laid out as a responsive definition list. */
export function DetailList({ items, columns = 2 }: { items: [string, ReactNode][]; columns?: number }) {
  return (
    <Box
      component="dl"
      sx={{
        m: 0,
        display: 'grid',
        gridTemplateColumns: { xs: '1fr', sm: `repeat(${columns}, minmax(0, 1fr))` },
        columnGap: 3,
        rowGap: 1.75,
      }}
    >
      {items.map(([label, value]) => (
        <Box key={label}>
          <Typography component="dt" variant="body2" color="text.secondary">
            {label}
          </Typography>
          <Typography component="dd" sx={{ m: 0, fontWeight: 550, overflowWrap: 'anywhere' }}>
            {value ?? '—'}
          </Typography>
        </Box>
      ))}
    </Box>
  );
}

export function Loading({ label = 'Loading' }: { label?: string }) {
  return (
    <Box sx={{ display: 'grid', placeItems: 'center', py: 8 }} role="status" aria-label={label}>
      <CircularProgress />
    </Box>
  );
}

export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const apiError = toApiError(error);
  const notFound = apiError.status === 404;
  return (
    <Alert
      severity={notFound ? 'info' : 'error'}
      action={onRetry && !notFound ? <Button color="inherit" onClick={onRetry}>Try again</Button> : undefined}
      sx={{ my: 2 }}
    >
      <AlertTitle>{notFound ? 'Not found' : 'This could not be loaded'}</AlertTitle>
      {notFound ? 'It may have been removed, or it belongs to another account.' : apiError.message}
    </Alert>
  );
}

export function EmptyState({ title, children, action }: { title: string; children?: ReactNode; action?: ReactNode }) {
  return (
    <Box sx={{ py: 6, px: 2, textAlign: 'center', border: '1px dashed', borderColor: 'divider', borderRadius: 1.5 }}>
      <Typography variant="h5" component="p">
        {title}
      </Typography>
      {children && (
        <Typography color="text.secondary" sx={{ mt: 1, maxWidth: 480, mx: 'auto' }}>
          {children}
        </Typography>
      )}
      {action && <Box sx={{ mt: 2.5 }}>{action}</Box>}
    </Box>
  );
}

/** Wraps a react-query result: spinner, error with retry, or the content. */
export function QueryView<T>({
  query,
  children,
}: {
  query: { data: T | undefined; isLoading: boolean; error: unknown; refetch: () => unknown };
  children: (data: T) => ReactNode;
}) {
  if (query.isLoading) return <Loading />;
  if (query.error) return <ErrorState error={query.error} onRetry={() => query.refetch()} />;
  if (query.data === undefined) return null;
  return <>{children(query.data)}</>;
}

export function StatTile({ label, value, hint }: { label: string; value: ReactNode; hint?: ReactNode }) {
  return (
    <Card sx={{ height: '100%' }}>
      <CardContent>
        <Typography variant="body2" color="text.secondary">
          {label}
        </Typography>
        <Typography sx={{ fontSize: '2rem', fontWeight: 800, fontStretch: '80%', lineHeight: 1.2, mt: 0.5 }}>{value}</Typography>
        {hint && (
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
            {hint}
          </Typography>
        )}
      </CardContent>
    </Card>
  );
}
