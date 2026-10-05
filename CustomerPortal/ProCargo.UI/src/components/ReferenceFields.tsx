import { useMemo } from 'react';
import { Controller, type Control, type FieldValues, type Path } from 'react-hook-form';
import { Autocomplete, TextField } from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { referenceApi } from '../api/endpoints';
import type { City } from '../api/types';

export const useReference = () =>
  useQuery({ queryKey: ['reference'], queryFn: referenceApi.reference, staleTime: 10 * 60_000 });

export const useCities = () => useQuery({ queryKey: ['cities'], queryFn: () => referenceApi.cities(), staleTime: 10 * 60_000 });

/** City picker (searchable, grouped by state) bound to a numeric cityId field. */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export function CityField<T extends FieldValues>({ control, name, label }: { control: Control<T, any, any>; name: Path<T>; label: string }) {
  const cities = useCities();
  const options = useMemo(
    () => [...(cities.data ?? [])].sort((a, b) => a.stateName.localeCompare(b.stateName) || a.name.localeCompare(b.name)),
    [cities.data],
  );

  return (
    <Controller
      control={control}
      name={name}
      render={({ field, fieldState }) => (
        <Autocomplete<City>
          options={options}
          loading={cities.isLoading}
          groupBy={(c) => c.stateName}
          getOptionLabel={(c) => c.name}
          isOptionEqualToValue={(a, b) => a.cityId === b.cityId}
          value={options.find((c) => c.cityId === field.value) ?? null}
          onChange={(_, c) => field.onChange(c?.cityId ?? 0)}
          onBlur={field.onBlur}
          renderInput={(params) => (
            <TextField {...params} label={label} error={!!fieldState.error} helperText={fieldState.error?.message} />
          )}
        />
      )}
    />
  );
}
