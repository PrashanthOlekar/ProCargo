import { useRef, useState, type ReactNode } from 'react';
import {
  Box,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TablePagination,
  TableRow,
  TextField,
  InputAdornment,
  Stack,
  useMediaQuery,
  useTheme,
  Card,
  CardActionArea,
  CardContent,
} from '@mui/material';
import SearchIcon from '@mui/icons-material/Search';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import type { PagedResult } from '../api/types';
import { EmptyState, ErrorState, Loading } from './Layout';

export interface Column<T> {
  header: string;
  render: (row: T) => ReactNode;
  align?: 'left' | 'right' | 'center';
  /** Hide on phones; the row card shows `primary` columns only. */
  primary?: boolean;
  width?: number | string;
}

interface Props<T> {
  queryKey: unknown[];
  fetcher: (page: { pageNumber: number; pageSize: number; search?: string }) => Promise<PagedResult<T>>;
  columns: Column<T>[];
  rowKey: (row: T) => string | number;
  onRowClick?: (row: T) => void;
  searchPlaceholder?: string;
  filters?: ReactNode;
  empty: { title: string; text?: string; action?: ReactNode };
}

/**
 * Server-paged list. On phones each row becomes a tappable card showing the primary columns, because a
 * nine-column table is unusable on a driver's or owner's phone.
 */
export function DataTable<T>({ queryKey, fetcher, columns, rowKey, onRowClick, searchPlaceholder, filters, empty }: Props<T>) {
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [search, setSearch] = useState('');
  const [debounced, setDebounced] = useState('');
  const timer = useRef<number | undefined>(undefined);
  const theme = useTheme();
  const phone = useMediaQuery(theme.breakpoints.down('sm'));

  const query = useQuery({
    queryKey: [...queryKey, pageNumber, pageSize, debounced],
    queryFn: () => fetcher({ pageNumber, pageSize, search: debounced || undefined }),
    placeholderData: keepPreviousData,
  });

  const onSearch = (value: string) => {
    setSearch(value);
    window.clearTimeout(timer.current);
    timer.current = window.setTimeout(() => {
      setDebounced(value.trim());
      setPageNumber(1);
    }, 350);
  };

  const rows = query.data?.items ?? [];

  return (
    <Box>
      {(searchPlaceholder || filters) && (
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} sx={{ mb: 2 }}>
          {searchPlaceholder && (
            <TextField
              value={search}
              onChange={(e) => onSearch(e.target.value)}
              placeholder={searchPlaceholder}
              slotProps={{
                input: { startAdornment: <InputAdornment position="start"><SearchIcon fontSize="small" /></InputAdornment> },
                htmlInput: { 'aria-label': searchPlaceholder },
              }}
              sx={{ maxWidth: { sm: 360 } }}
            />
          )}
          {filters}
        </Stack>
      )}

      {query.isLoading && <Loading />}
      {query.error && <ErrorState error={query.error} onRetry={() => query.refetch()} />}

      {!query.isLoading && !query.error && rows.length === 0 && (
        <EmptyState title={debounced ? 'Nothing matches your search' : empty.title} action={debounced ? undefined : empty.action}>
          {debounced ? 'Try a different number or name.' : empty.text}
        </EmptyState>
      )}

      {rows.length > 0 && !phone && (
        <TableContainer sx={{ border: 1, borderColor: 'divider', borderRadius: 1.5, bgcolor: 'background.paper' }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                {columns.map((c) => (
                  <TableCell key={c.header} align={c.align} sx={{ width: c.width }}>
                    {c.header}
                  </TableCell>
                ))}
              </TableRow>
            </TableHead>
            <TableBody>
              {rows.map((row) => (
                <TableRow
                  key={rowKey(row)}
                  hover={!!onRowClick}
                  onClick={onRowClick ? () => onRowClick(row) : undefined}
                  onKeyDown={onRowClick ? (e) => e.key === 'Enter' && onRowClick(row) : undefined}
                  tabIndex={onRowClick ? 0 : undefined}
                  sx={{ cursor: onRowClick ? 'pointer' : 'default', '& td': { py: 1.25 } }}
                >
                  {columns.map((c) => (
                    <TableCell key={c.header} align={c.align}>
                      {c.render(row)}
                    </TableCell>
                  ))}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {rows.length > 0 && phone && (
        <Stack spacing={1.25}>
          {rows.map((row) => {
            const content = (
              <CardContent sx={{ display: 'grid', gap: 0.75 }}>
                {columns.filter((c) => c.primary).map((c) => (
                  <Box key={c.header}>{c.render(row)}</Box>
                ))}
              </CardContent>
            );
            return (
              <Card key={rowKey(row)}>{onRowClick ? <CardActionArea onClick={() => onRowClick(row)}>{content}</CardActionArea> : content}</Card>
            );
          })}
        </Stack>
      )}

      {query.data && query.data.totalRecords > pageSize && (
        <TablePagination
          component="div"
          count={query.data.totalRecords}
          page={pageNumber - 1}
          rowsPerPage={pageSize}
          rowsPerPageOptions={[10, 20, 50]}
          onPageChange={(_, p) => setPageNumber(p + 1)}
          onRowsPerPageChange={(e) => {
            setPageSize(Number(e.target.value));
            setPageNumber(1);
          }}
        />
      )}
    </Box>
  );
}
