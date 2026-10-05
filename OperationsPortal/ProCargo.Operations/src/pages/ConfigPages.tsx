import { useState, type ReactNode } from 'react';
import { Box, Button, Chip, IconButton, Tab, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Tabs, Typography } from '@mui/material';
import EditOutlined from '@mui/icons-material/EditOutlined';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { adminApi, masterDataApi, pricingApi } from '../api/endpoints';
import { P, useAuth } from '../auth/AuthContext';
import { useNotify } from '../components/Forms';
import { PageHeader, QueryView, Section } from '../components/Layout';
import { RecordDialog, type FieldSpec } from '../components/Records';
import { useReference, useCities } from '../components/ReferenceFields';
import { formatDate, formatMoney, toLocalInput } from '../lib/format';

interface ColumnDef<T> {
  header: string;
  render: (row: T) => ReactNode;
  align?: 'right';
}

/** A configuration table with add and edit through a RecordDialog. */
function ConfigTable<T>({
  title,
  rows,
  rowKey,
  columns,
  fields,
  toForm,
  empty,
  save,
  refreshKey,
  note,
}: {
  title: string;
  rows: T[];
  rowKey: (r: T) => number | string;
  columns: ColumnDef<T>[];
  fields: FieldSpec[];
  toForm: (r: T | null) => Record<string, unknown>;
  empty: string;
  save: (row: T | null, values: Record<string, unknown>) => Promise<unknown>;
  refreshKey: unknown[];
  note?: string;
}) {
  const [editing, setEditing] = useState<T | 'new' | null>(null);
  const notify = useNotify();
  const queryClient = useQueryClient();

  return (
    <Section title={title} action={<Button variant="contained" onClick={() => setEditing('new')}>Add</Button>}>
      {note && <Typography color="text.secondary" sx={{ mb: 2 }}>{note}</Typography>}
      {rows.length === 0 ? (
        <Typography color="text.secondary">{empty}</Typography>
      ) : (
        <TableContainer>
          <Table size="small">
            <TableHead>
              <TableRow>
                {columns.map((c) => <TableCell key={c.header} align={c.align}>{c.header}</TableCell>)}
                <TableCell />
              </TableRow>
            </TableHead>
            <TableBody>
              {rows.map((r) => (
                <TableRow key={rowKey(r)}>
                  {columns.map((c) => <TableCell key={c.header} align={c.align}>{c.render(r)}</TableCell>)}
                  <TableCell align="right">
                    <IconButton size="small" aria-label="Edit" onClick={() => setEditing(r)}><EditOutlined fontSize="small" /></IconButton>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
      {editing && (
        <RecordDialog
          title={editing === 'new' ? `Add to ${title.toLowerCase()}` : `Edit ${title.toLowerCase()}`}
          fields={fields}
          initial={toForm(editing === 'new' ? null : editing)}
          onClose={() => setEditing(null)}
          onSubmit={async (values) => {
            await save(editing === 'new' ? null : editing, values);
            notify('Saved');
            setEditing(null);
            queryClient.invalidateQueries({ queryKey: refreshKey });
          }}
        />
      )}
    </Section>
  );
}

const active = (v: boolean) => (v ? <Chip size="small" label="Active" color="success" variant="outlined" /> : <Chip size="small" label="Inactive" variant="outlined" />);
const nowLocal = () => toLocalInput(new Date().toISOString());
const dateTimeLocal = (iso: string | null | undefined) => (iso ? toLocalInput(iso) : '');

// ---------------- pricing ----------------

export function PricingPage() {
  const query = useQuery({ queryKey: ['pricing'], queryFn: pricingApi.configuration });
  const reference = useReference();
  const cities = useCities();
  const [tab, setTab] = useState(0);
  const vehicleOptions = (reference.data?.vehicleTypes ?? []).map((v) => ({ value: v.vehicleTypeId, label: v.name }));
  const anyVehicle = [{ value: '', label: 'All vehicle types' }, ...vehicleOptions];
  const cityOptions = [{ value: '', label: 'Any' }, ...(cities.data ?? []).map((c) => ({ value: c.cityId, label: c.name }))];
  const effective: FieldSpec[] = [
    { name: 'effectiveFromUtc', label: 'Effective from', type: 'datetime', required: true, width: 6 },
    { name: 'effectiveToUtc', label: 'Effective to (optional)', type: 'datetime', width: 6 },
    { name: 'isActive', label: 'Active', type: 'checkbox' },
  ];

  return (
    <>
      <PageHeader title="Pricing" subtitle="Every quotation and estimate is calculated from these tables. Changes apply to new quotations only; sent quotations keep their price." />
      <Tabs value={tab} onChange={(_, t) => setTab(t)} variant="scrollable" sx={{ mb: 2 }}>
        <Tab label="Vehicle rate cards" />
        <Tab label="Distance slabs" />
        <Tab label="Additional charges" />
        <Tab label="Route & season rules" />
        <Tab label="Tax" />
        <Tab label="Owner commission" />
      </Tabs>
      <QueryView query={query}>
        {(c) => (
          <>
            {tab === 0 && (
              <ConfigTable
                title="Vehicle rate cards"
                rows={c.vehiclePricing}
                rowKey={(r) => r.vehiclePricingId}
                refreshKey={['pricing']}
                empty="No rate cards yet."
                note="Base fare plus per-km and per-kg rates, with a minimum fare. Waiting is free up to the given hours."
                columns={[
                  { header: 'Vehicle', render: (r) => r.vehicleTypeName },
                  { header: 'Base fare', align: 'right', render: (r) => formatMoney(r.baseFare) },
                  { header: 'Per km', align: 'right', render: (r) => formatMoney(r.perKmRate) },
                  { header: 'Per kg', align: 'right', render: (r) => `₹${r.perKgRate}` },
                  { header: 'Minimum', align: 'right', render: (r) => formatMoney(r.minimumFare) },
                  { header: 'Free waiting', align: 'right', render: (r) => `${r.freeWaitingHours} h` },
                  { header: 'From', render: (r) => formatDate(r.effectiveFromUtc) },
                  { header: '', render: (r) => active(r.isActive) },
                ]}
                fields={[
                  { name: 'vehicleTypeId', label: 'Vehicle type', type: 'select', options: vehicleOptions, required: true },
                  { name: 'baseFare', label: 'Base fare (₹)', type: 'number', required: true, width: 6 },
                  { name: 'minimumFare', label: 'Minimum fare (₹)', type: 'number', required: true, width: 6 },
                  { name: 'perKmRate', label: 'Per km (₹)', type: 'number', required: true, width: 4 },
                  { name: 'perKgRate', label: 'Per kg (₹)', type: 'number', required: true, width: 4 },
                  { name: 'freeWaitingHours', label: 'Free waiting (h)', type: 'number', required: true, width: 4 },
                  ...effective,
                ]}
                toForm={(r) => ({ vehicleTypeId: r?.vehicleTypeId ?? '', baseFare: r?.baseFare ?? '', minimumFare: r?.minimumFare ?? '', perKmRate: r?.perKmRate ?? '', perKgRate: r?.perKgRate ?? 0, freeWaitingHours: r?.freeWaitingHours ?? 2, effectiveFromUtc: r ? dateTimeLocal(r.effectiveFromUtc) : nowLocal(), effectiveToUtc: dateTimeLocal(r?.effectiveToUtc), isActive: r?.isActive ?? true })}
                save={(r, v) => pricingApi.save('vehicle-rates', r?.vehiclePricingId ?? null, v)}
              />
            )}
            {tab === 1 && (
              <ConfigTable
                title="Distance slabs"
                rows={c.distanceSlabs}
                rowKey={(r) => r.distancePricingId}
                refreshKey={['pricing']}
                empty="No slabs: the rate card's per-km rate applies to the whole distance."
                note="Tapered per-km rates: each part of the distance is charged at its slab's rate."
                columns={[
                  { header: 'Vehicle', render: (r) => r.vehicleTypeName },
                  { header: 'From km', align: 'right', render: (r) => r.fromKm },
                  { header: 'To km', align: 'right', render: (r) => r.toKm ?? 'and above' },
                  { header: 'Rate per km', align: 'right', render: (r) => formatMoney(r.ratePerKm) },
                  { header: '', render: (r) => active(r.isActive) },
                ]}
                fields={[
                  { name: 'vehicleTypeId', label: 'Vehicle type', type: 'select', options: vehicleOptions, required: true },
                  { name: 'fromKm', label: 'From km', type: 'number', required: true, width: 4 },
                  { name: 'toKm', label: 'To km (blank = no limit)', type: 'number', width: 4 },
                  { name: 'ratePerKm', label: 'Rate per km (₹)', type: 'number', required: true, width: 4 },
                  { name: 'isActive', label: 'Active', type: 'checkbox' },
                ]}
                toForm={(r) => ({ vehicleTypeId: r?.vehicleTypeId ?? '', fromKm: r?.fromKm ?? '', toKm: r?.toKm ?? '', ratePerKm: r?.ratePerKm ?? '', isActive: r?.isActive ?? true })}
                save={(r, v) => pricingApi.save('distance-slabs', r?.distancePricingId ?? null, v)}
              />
            )}
            {tab === 2 && (
              <ConfigTable
                title="Additional charges"
                rows={c.additionalCharges}
                rowKey={(r) => r.additionalChargeId}
                refreshKey={['pricing']}
                empty="No additional charges."
                note="Codes used by the calculator: LOADING, UNLOADING, WAITING (per hour), NIGHT, SPECIAL_HANDLING. Percentage charges apply to the freight."
                columns={[
                  { header: 'Code', render: (r) => r.chargeCode },
                  { header: 'Name', render: (r) => r.name },
                  { header: 'Vehicle', render: (r) => r.vehicleTypeName ?? 'All' },
                  { header: 'Type', render: (r) => r.calculationType },
                  { header: 'Amount', align: 'right', render: (r) => (r.calculationType === 'Percentage' ? `${r.amount}%` : formatMoney(r.amount)) },
                  { header: '', render: (r) => active(r.isActive) },
                ]}
                fields={[
                  { name: 'chargeCode', label: 'Code', required: true, width: 6 },
                  { name: 'name', label: 'Name on quotation', required: true, width: 6 },
                  { name: 'vehicleTypeId', label: 'Vehicle type', type: 'select', options: anyVehicle },
                  { name: 'calculationType', label: 'Calculation', type: 'select', required: true, width: 6, options: [{ value: 'Flat', label: 'Flat amount' }, { value: 'Percentage', label: '% of freight' }, { value: 'PerHour', label: 'Per hour' }, { value: 'PerKm', label: 'Per km' }] },
                  { name: 'amount', label: 'Amount or %', type: 'number', required: true, width: 6 },
                  { name: 'isActive', label: 'Active', type: 'checkbox' },
                ]}
                toForm={(r) => ({ chargeCode: r?.chargeCode ?? '', name: r?.name ?? '', vehicleTypeId: r?.vehicleTypeId ?? '', calculationType: r?.calculationType ?? 'Flat', amount: r?.amount ?? '', isActive: r?.isActive ?? true })}
                save={(r, v) => pricingApi.save('additional-charges', r?.additionalChargeId ?? null, v)}
              />
            )}
            {tab === 3 && (
              <ConfigTable
                title="Pricing rules"
                rows={c.rules}
                rowKey={(r) => r.pricingRuleId}
                refreshKey={['pricing']}
                empty="No rules."
                note="Surcharges or discounts for a lane, state, city or season. Lower priority numbers apply first."
                columns={[
                  { header: 'Rule', render: (r) => r.name },
                  { header: 'Applies to', render: (r) => [r.vehicleTypeName, r.pickupCityName && `from ${r.pickupCityName}`, r.deliveryCityName && `to ${r.deliveryCityName}`, r.cityName, r.stateName].filter(Boolean).join(', ') || 'Everything' },
                  { header: 'Adjustment', align: 'right', render: (r) => (r.adjustmentType === 'Percentage' ? `${r.adjustmentValue}%` : formatMoney(r.adjustmentValue)) },
                  { header: 'Priority', align: 'right', render: (r) => r.priority },
                  { header: 'Period', render: (r) => `${formatDate(r.effectiveFromUtc)} – ${r.effectiveToUtc ? formatDate(r.effectiveToUtc) : 'open'}` },
                  { header: '', render: (r) => active(r.isActive) },
                ]}
                fields={[
                  { name: 'name', label: 'Name', required: true },
                  { name: 'vehicleTypeId', label: 'Vehicle type', type: 'select', options: anyVehicle, width: 6 },
                  { name: 'priority', label: 'Priority', type: 'number', required: true, width: 6 },
                  { name: 'pickupCityId', label: 'Pickup city', type: 'select', options: cityOptions, width: 6 },
                  { name: 'deliveryCityId', label: 'Delivery city', type: 'select', options: cityOptions, width: 6 },
                  { name: 'adjustmentType', label: 'Type', type: 'select', required: true, width: 6, options: [{ value: 'Percentage', label: '% of freight' }, { value: 'Flat', label: 'Flat amount' }] },
                  { name: 'adjustmentValue', label: 'Value (negative for a discount)', type: 'number', required: true, width: 6 },
                  ...effective,
                ]}
                toForm={(r) => ({ name: r?.name ?? '', vehicleTypeId: r?.vehicleTypeId ?? '', priority: r?.priority ?? 100, pickupCityId: r?.pickupCityId ?? '', deliveryCityId: r?.deliveryCityId ?? '', adjustmentType: r?.adjustmentType ?? 'Percentage', adjustmentValue: r?.adjustmentValue ?? '', effectiveFromUtc: r ? dateTimeLocal(r.effectiveFromUtc) : nowLocal(), effectiveToUtc: dateTimeLocal(r?.effectiveToUtc), isActive: r?.isActive ?? true })}
                save={(r, v) => pricingApi.save('rules', r?.pricingRuleId ?? null, { ...v, stateId: r?.stateId ?? null, cityId: r?.cityId ?? null })}
              />
            )}
            {tab === 4 && (
              <ConfigTable
                title="Tax rates"
                rows={c.taxRates}
                rowKey={(r) => r.taxRateId}
                refreshKey={['pricing']}
                empty="No tax rates."
                note="GST_GTA is applied to quotations and invoices for goods transport."
                columns={[
                  { header: 'Code', render: (r) => r.code },
                  { header: 'Name', render: (r) => r.name },
                  { header: 'Rate', align: 'right', render: (r) => `${r.ratePercent}%` },
                  { header: 'From', render: (r) => formatDate(r.effectiveFromUtc) },
                  { header: '', render: (r) => active(r.isActive) },
                ]}
                fields={[
                  { name: 'code', label: 'Code', required: true, width: 6 },
                  { name: 'ratePercent', label: 'Rate %', type: 'number', required: true, width: 6 },
                  { name: 'name', label: 'Name', required: true },
                  ...effective,
                ]}
                toForm={(r) => ({ code: r?.code ?? 'GST_GTA', name: r?.name ?? '', ratePercent: r?.ratePercent ?? '', effectiveFromUtc: r ? dateTimeLocal(r.effectiveFromUtc) : nowLocal(), effectiveToUtc: dateTimeLocal(r?.effectiveToUtc), isActive: r?.isActive ?? true })}
                save={(r, v) => pricingApi.save('tax-rates', r?.taxRateId ?? null, v)}
              />
            )}
            {tab === 5 && (
              <ConfigTable
                title="Commission rules"
                rows={c.commissionRules}
                rowKey={(r) => r.commissionRuleId}
                refreshKey={['pricing']}
                empty="No commission rules: settlements cannot be prepared."
                note="Platform commission deducted from the freight in owner settlements, with a minimum per trip."
                columns={[
                  { header: 'Rule', render: (r) => r.name },
                  { header: 'Vehicle', render: (r) => r.vehicleTypeName ?? 'All' },
                  { header: 'Commission', align: 'right', render: (r) => `${r.commissionPercent}%` },
                  { header: 'Minimum', align: 'right', render: (r) => formatMoney(r.minimumCommission) },
                  { header: 'From', render: (r) => formatDate(r.effectiveFromUtc) },
                  { header: '', render: (r) => active(r.isActive) },
                ]}
                fields={[
                  { name: 'name', label: 'Name', required: true },
                  { name: 'vehicleTypeId', label: 'Vehicle type', type: 'select', options: anyVehicle },
                  { name: 'commissionPercent', label: 'Commission %', type: 'number', required: true, width: 6 },
                  { name: 'minimumCommission', label: 'Minimum (₹)', type: 'number', required: true, width: 6 },
                  ...effective,
                ]}
                toForm={(r) => ({ name: r?.name ?? '', vehicleTypeId: r?.vehicleTypeId ?? '', commissionPercent: r?.commissionPercent ?? '', minimumCommission: r?.minimumCommission ?? 0, effectiveFromUtc: r ? dateTimeLocal(r.effectiveFromUtc) : nowLocal(), effectiveToUtc: dateTimeLocal(r?.effectiveToUtc), isActive: r?.isActive ?? true })}
                save={(r, v) => pricingApi.save('commission-rules', r?.commissionRuleId ?? null, v)}
              />
            )}
          </>
        )}
      </QueryView>
    </>
  );
}

// ---------------- master data & settings ----------------

export function SettingsPage() {
  const { can } = useAuth();
  const tabs = [
    can(P.ManageMasterData) && 'Vehicle types',
    can(P.ManageMasterData) && 'Goods types',
    can(P.ManageMasterData) && 'Cities',
    can(P.ManageMasterData) && 'States',
    can(P.ManageMasterData) && 'Document types',
    can(P.ManageSystemSettings) && 'Business settings',
    can(P.ManageNotificationTemplates) && 'Message templates',
  ].filter(Boolean) as string[];
  const [tab, setTab] = useState(0);
  const current = tabs[tab];

  return (
    <>
      <PageHeader title="Master data & settings" />
      <Tabs value={tab} onChange={(_, t) => setTab(t)} variant="scrollable" sx={{ mb: 2 }}>
        {tabs.map((t) => <Tab key={t} label={t} />)}
      </Tabs>
      {current === 'Vehicle types' && <VehicleTypes />}
      {current === 'Goods types' && <GoodsTypes />}
      {current === 'Cities' && <Cities />}
      {current === 'States' && <States />}
      {current === 'Document types' && <DocumentTypes />}
      {current === 'Business settings' && <BusinessSettings />}
      {current === 'Message templates' && <Templates />}
    </>
  );
}

function VehicleTypes() {
  const q = useQuery({ queryKey: ['md', 'vehicleTypes'], queryFn: masterDataApi.vehicleTypes });
  return (
    <QueryView query={q}>
      {(rows) => (
        <ConfigTable
          title="Vehicle types"
          rows={rows}
          rowKey={(r) => r.vehicleTypeId}
          refreshKey={['md', 'vehicleTypes']}
          empty="None."
          columns={[
            { header: 'Code', render: (r) => r.code },
            { header: 'Name', render: (r) => r.name },
            { header: 'Capacity', align: 'right', render: (r) => `${r.capacityKg} kg` },
            { header: 'Length', align: 'right', render: (r) => (r.lengthFt ? `${r.lengthFt} ft` : '—') },
            { header: 'Order', align: 'right', render: (r) => r.sortOrder },
            { header: '', render: (r) => active(r.isActive) },
          ]}
          fields={[
            { name: 'code', label: 'Code', required: true, width: 6 },
            { name: 'name', label: 'Name', required: true, width: 6 },
            { name: 'capacityKg', label: 'Capacity (kg)', type: 'number', required: true, width: 4 },
            { name: 'lengthFt', label: 'Body length (ft)', type: 'number', width: 4 },
            { name: 'sortOrder', label: 'Display order', type: 'number', width: 4 },
            { name: 'description', label: 'Description', type: 'multiline' },
            { name: 'isActive', label: 'Active', type: 'checkbox' },
          ]}
          toForm={(r) => ({ code: r?.code ?? '', name: r?.name ?? '', capacityKg: r?.capacityKg ?? '', lengthFt: r?.lengthFt ?? '', sortOrder: r?.sortOrder ?? 0, description: r?.description ?? '', isActive: r?.isActive ?? true })}
          save={(r, v) => masterDataApi.saveVehicleType(r?.vehicleTypeId ?? null, { ...v, sortOrder: v.sortOrder ?? 0 })}
        />
      )}
    </QueryView>
  );
}

function GoodsTypes() {
  const q = useQuery({ queryKey: ['md', 'goodsTypes'], queryFn: masterDataApi.goodsTypes });
  return (
    <QueryView query={q}>
      {(rows) => (
        <ConfigTable
          title="Goods types"
          rows={rows}
          rowKey={(r) => r.goodsTypeId}
          refreshKey={['md', 'goodsTypes']}
          empty="None."
          columns={[
            { header: 'Code', render: (r) => r.code },
            { header: 'Name', render: (r) => r.name },
            { header: 'Special handling', render: (r) => (r.requiresSpecialHandling ? 'Yes' : 'No') },
            { header: '', render: (r) => active(r.isActive) },
          ]}
          fields={[
            { name: 'code', label: 'Code', required: true, width: 6 },
            { name: 'name', label: 'Name', required: true, width: 6 },
            { name: 'requiresSpecialHandling', label: 'Requires special handling (surcharge applies)', type: 'checkbox' },
            { name: 'isActive', label: 'Active', type: 'checkbox' },
          ]}
          toForm={(r) => ({ code: r?.code ?? '', name: r?.name ?? '', requiresSpecialHandling: r?.requiresSpecialHandling ?? false, isActive: r?.isActive ?? true })}
          save={(r, v) => masterDataApi.saveGoodsType(r?.goodsTypeId ?? null, v)}
        />
      )}
    </QueryView>
  );
}

function States() {
  const q = useQuery({ queryKey: ['md', 'states'], queryFn: masterDataApi.states });
  return (
    <QueryView query={q}>
      {(rows) => (
        <ConfigTable
          title="States"
          rows={rows}
          rowKey={(r) => r.stateId}
          refreshKey={['md', 'states']}
          empty="None."
          columns={[{ header: 'Code', render: (r) => r.stateCode }, { header: 'Name', render: (r) => r.name }, { header: '', render: (r) => active(r.isActive) }]}
          fields={[{ name: 'stateCode', label: 'Two-letter code', required: true, width: 6 }, { name: 'name', label: 'Name', required: true, width: 6 }, { name: 'isActive', label: 'Active', type: 'checkbox' }]}
          toForm={(r) => ({ stateCode: r?.stateCode ?? '', name: r?.name ?? '', isActive: r?.isActive ?? true })}
          save={(r, v) => masterDataApi.saveState(r?.stateId ?? null, v)}
        />
      )}
    </QueryView>
  );
}

function Cities() {
  const q = useQuery({ queryKey: ['md', 'cities'], queryFn: masterDataApi.cities });
  const states = useQuery({ queryKey: ['md', 'states'], queryFn: masterDataApi.states });
  return (
    <QueryView query={q}>
      {(rows) => (
        <ConfigTable
          title="Cities"
          rows={rows}
          rowKey={(r) => r.cityId}
          refreshKey={['md', 'cities']}
          empty="None."
          columns={[{ header: 'City', render: (r) => r.name }, { header: 'State', render: (r) => r.stateName }, { header: '', render: (r) => active(r.isActive) }]}
          fields={[
            { name: 'stateId', label: 'State', type: 'select', required: true, options: (states.data ?? []).map((s) => ({ value: s.stateId, label: s.name })) },
            { name: 'name', label: 'City', required: true },
            { name: 'isActive', label: 'Active', type: 'checkbox' },
          ]}
          toForm={(r) => ({ stateId: r?.stateId ?? '', name: r?.name ?? '', isActive: r?.isActive ?? true })}
          save={(r, v) => masterDataApi.saveCity(r?.cityId ?? null, v)}
        />
      )}
    </QueryView>
  );
}

function DocumentTypes() {
  const q = useQuery({ queryKey: ['md', 'documentTypes'], queryFn: masterDataApi.documentTypes });
  return (
    <QueryView query={q}>
      {(rows) => (
        <ConfigTable
          title="Document types"
          rows={rows}
          rowKey={(r) => r.documentTypeId}
          refreshKey={['md', 'documentTypes']}
          empty="None."
          columns={[
            { header: 'Code', render: (r) => r.code },
            { header: 'Name', render: (r) => r.name },
            { header: 'For', render: (r) => r.appliesTo },
            { header: 'Mandatory', render: (r) => (r.isMandatory ? 'Yes' : 'No') },
            { header: 'Has expiry', render: (r) => (r.requiresExpiry ? 'Yes' : 'No') },
            { header: '', render: (r) => active(r.isActive) },
          ]}
          fields={[
            { name: 'code', label: 'Code', required: true, width: 6 },
            { name: 'appliesTo', label: 'For', type: 'select', required: true, width: 6, options: ['Customer', 'Owner', 'Driver', 'Vehicle'].map((x) => ({ value: x, label: x })) },
            { name: 'name', label: 'Name', required: true },
            { name: 'isMandatory', label: 'Required for verification', type: 'checkbox' },
            { name: 'requiresExpiry', label: 'Has an expiry date', type: 'checkbox' },
            { name: 'isActive', label: 'Active', type: 'checkbox' },
          ]}
          toForm={(r) => ({ code: r?.code ?? '', appliesTo: r?.appliesTo ?? 'Vehicle', name: r?.name ?? '', isMandatory: r?.isMandatory ?? false, requiresExpiry: r?.requiresExpiry ?? false, isActive: r?.isActive ?? true })}
          save={(r, v) => masterDataApi.saveDocumentType(r?.documentTypeId ?? null, v)}
        />
      )}
    </QueryView>
  );
}

function BusinessSettings() {
  const q = useQuery({ queryKey: ['settings'], queryFn: masterDataApi.settings });
  const [editing, setEditing] = useState<string | null>(null);
  const notify = useNotify();
  const queryClient = useQueryClient();
  return (
    <Section title="Business settings">
      <QueryView query={q}>
        {(rows) => (
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Setting</TableCell>
                <TableCell>Value</TableCell>
                <TableCell>Changed</TableCell>
                <TableCell />
              </TableRow>
            </TableHead>
            <TableBody>
              {rows.map((s) => (
                <TableRow key={s.settingKey}>
                  <TableCell>
                    <Typography sx={{ fontWeight: 600 }}>{s.description ?? s.settingKey}</Typography>
                    <Typography variant="body2" color="text.secondary">{s.settingKey}</Typography>
                  </TableCell>
                  <TableCell>{s.settingValue}</TableCell>
                  <TableCell>{formatDate(s.modifiedDateUtc)}</TableCell>
                  <TableCell align="right">
                    {s.isEditable && <IconButton size="small" aria-label={`Edit ${s.settingKey}`} onClick={() => setEditing(s.settingKey)}><EditOutlined fontSize="small" /></IconButton>}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </QueryView>
      {editing && (
        <RecordDialog
          title={`Change ${editing}`}
          fields={[{ name: 'value', label: 'Value', required: true, helper: q.data?.find((s) => s.settingKey === editing)?.dataType }]}
          initial={{ value: q.data?.find((s) => s.settingKey === editing)?.settingValue ?? '' }}
          onClose={() => setEditing(null)}
          onSubmit={async (v) => {
            await masterDataApi.saveSetting(editing, String(v.value));
            notify('Setting saved');
            setEditing(null);
            queryClient.invalidateQueries({ queryKey: ['settings'] });
          }}
        />
      )}
    </Section>
  );
}

const channelName: Record<number, string> = { 1: 'In-app', 2: 'E-mail', 3: 'SMS', 4: 'Push' };

function Templates() {
  const q = useQuery({ queryKey: ['templates'], queryFn: adminApi.templates });
  const [editing, setEditing] = useState<number | null>(null);
  const notify = useNotify();
  const queryClient = useQueryClient();
  const current = q.data?.find((t) => t.notificationTemplateId === editing);
  return (
    <Section title="Message templates">
      <Typography color="text.secondary" sx={{ mb: 2 }}>
        Use {'{{Placeholder}}'} names exactly as they appear. Values are inserted safely; HTML is allowed in e-mail templates only.
      </Typography>
      <QueryView query={q}>
        {(rows) => (
          <Table size="small">
            <TableBody>
              {rows.map((t) => (
                <TableRow key={t.notificationTemplateId}>
                  <TableCell sx={{ width: 220 }}>
                    <Typography sx={{ fontWeight: 600 }}>{t.templateCode}</Typography>
                    <Typography variant="body2" color="text.secondary">{channelName[t.notificationChannelId]}{t.isActive ? '' : ' (off)'}</Typography>
                  </TableCell>
                  <TableCell>
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>{t.subject}</Typography>
                    <Box component="code" sx={{ fontSize: '0.8rem', color: 'text.secondary', display: 'block', whiteSpace: 'pre-wrap' }}>{t.body}</Box>
                  </TableCell>
                  <TableCell align="right">
                    <IconButton size="small" aria-label={`Edit ${t.templateCode}`} onClick={() => setEditing(t.notificationTemplateId)}><EditOutlined fontSize="small" /></IconButton>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </QueryView>
      {current && (
        <RecordDialog
          title={`${current.templateCode} (${channelName[current.notificationChannelId]})`}
          fields={[
            { name: 'subject', label: 'Subject / title', required: true },
            { name: 'body', label: 'Body', type: 'multiline', required: true },
            { name: 'isActive', label: 'Send this message', type: 'checkbox' },
          ]}
          initial={{ subject: current.subject, body: current.body, isActive: current.isActive }}
          onClose={() => setEditing(null)}
          onSubmit={async (v) => {
            await adminApi.saveTemplate(current.notificationTemplateId, v);
            notify('Template saved');
            setEditing(null);
            queryClient.invalidateQueries({ queryKey: ['templates'] });
          }}
        />
      )}
    </Section>
  );
}
