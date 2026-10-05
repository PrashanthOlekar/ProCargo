import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactElement } from 'react';
import { theme } from '../theme';
import { PlateTag } from '../components/PlateTag';
import { RecordDialog } from '../components/Records';

const wrap = (ui: ReactElement) =>
  render(
    <QueryClientProvider client={new QueryClient()}>
      <ThemeProvider theme={theme}>{ui}</ThemeProvider>
    </QueryClientProvider>,
  );

describe('components', () => {
  it('renders reference numbers as plates', () => {
    wrap(<PlateTag>PC-TRP-2026-000001</PlateTag>);
    expect(screen.getByText('PC-TRP-2026-000001')).toBeInTheDocument();
  });

  it('record dialog blocks empty required fields and converts numbers', async () => {
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    wrap(
      <RecordDialog
        title="Add rate"
        fields={[
          { name: 'name', label: 'Name', required: true },
          { name: 'amount', label: 'Amount', type: 'number', required: true },
          { name: 'isActive', label: 'Active', type: 'checkbox' },
        ]}
        initial={{ name: '', amount: '', isActive: true }}
        onSubmit={onSubmit}
        onClose={() => undefined}
      />,
    );
    const user = userEvent.setup();
    await user.click(screen.getByRole('button', { name: 'Save' }));
    expect(onSubmit).not.toHaveBeenCalled();
    expect(await screen.findByText('Enter name')).toBeInTheDocument();

    await user.type(screen.getByLabelText('Name'), 'Loading');
    await user.type(screen.getByLabelText('Amount'), '250');
    await user.click(screen.getByRole('button', { name: 'Save' }));
    expect(onSubmit).toHaveBeenCalledWith({ name: 'Loading', amount: 250, isActive: true });
  });
});
