import { useState } from 'react';
import { MenuItem, TextField } from '@mui/material';
import { useSearchParams } from 'react-router-dom';

/** A select filter kept in the URL (?key=value) so filtered lists can be linked from the dashboard and bookmarked. */
export function useUrlFilter(key: string) {
  const [params, setParams] = useSearchParams();
  const value = params.get(key) ?? '';
  const set = (next: string) => {
    const copy = new URLSearchParams(params);
    if (next) copy.set(key, next);
    else copy.delete(key);
    setParams(copy, { replace: true });
  };
  return [value, set] as const;
}

export function FilterSelect({
  label,
  value,
  onChange,
  options,
  width = 200,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  options: { value: string | number; label: string }[];
  width?: number;
}) {
  return (
    <TextField select label={label} value={value} onChange={(e) => onChange(e.target.value)} sx={{ width: { xs: '100%', sm: width } }}>
      <MenuItem value="">All</MenuItem>
      {options.map((o) => (
        <MenuItem key={o.value} value={String(o.value)}>
          {o.label}
        </MenuItem>
      ))}
    </TextField>
  );
}

export const statusOptions = (map: Record<number, { label: string }>) =>
  Object.entries(map).map(([id, s]) => ({ value: id, label: s.label }));

/** Tracks one open dialog by name. */
export function useDialog<T extends string>() {
  const [open, setOpen] = useState<T | null>(null);
  return { open, show: (name: T) => setOpen(name), close: () => setOpen(null), is: (name: T) => open === name };
}
