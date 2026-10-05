import { useState, type ReactNode } from 'react';
import { Link as RouterLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import {
  AppBar,
  Badge,
  Box,
  Button,
  Container,
  Drawer,
  IconButton,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  ListSubheader,
  Menu,
  MenuItem,
  Stack,
  Toolbar,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import MenuIcon from '@mui/icons-material/Menu';
import NotificationsOutlined from '@mui/icons-material/NotificationsOutlined';
import SpaceDashboardOutlined from '@mui/icons-material/SpaceDashboardOutlined';
import Inventory2Outlined from '@mui/icons-material/Inventory2Outlined';
import RequestQuoteOutlined from '@mui/icons-material/RequestQuoteOutlined';
import RouteOutlined from '@mui/icons-material/RouteOutlined';
import PeopleOutline from '@mui/icons-material/PeopleOutline';
import LocalShippingOutlined from '@mui/icons-material/LocalShippingOutlined';
import BadgeOutlined from '@mui/icons-material/BadgeOutlined';
import StoreOutlined from '@mui/icons-material/StoreOutlined';
import ReceiptLongOutlined from '@mui/icons-material/ReceiptLongOutlined';
import PaymentsOutlined from '@mui/icons-material/PaymentsOutlined';
import AccountBalanceOutlined from '@mui/icons-material/AccountBalanceOutlined';
import PriceChangeOutlined from '@mui/icons-material/PriceChangeOutlined';
import SupportAgentOutlined from '@mui/icons-material/SupportAgentOutlined';
import ReportProblemOutlined from '@mui/icons-material/ReportProblemOutlined';
import BarChartOutlined from '@mui/icons-material/BarChartOutlined';
import ManageAccountsOutlined from '@mui/icons-material/ManageAccountsOutlined';
import AdminPanelSettingsOutlined from '@mui/icons-material/AdminPanelSettingsOutlined';
import TuneOutlined from '@mui/icons-material/TuneOutlined';
import HistoryOutlined from '@mui/icons-material/HistoryOutlined';
import MailOutline from '@mui/icons-material/MailOutline';
import { useQuery } from '@tanstack/react-query';
import { notificationApi } from '../api/endpoints';
import { P, useAuth } from '../auth/AuthContext';
import { palette } from '../theme';

interface NavItem {
  to: string;
  label: string;
  icon: ReactNode;
  anyOf?: string[];
}

const sections: { title: string; items: NavItem[] }[] = [
  {
    title: 'Desk',
    items: [
      { to: '/', label: 'Dashboard', icon: <SpaceDashboardOutlined /> },
      { to: '/bookings', label: 'Bookings', icon: <Inventory2Outlined />, anyOf: [P.ViewBookings] },
      { to: '/quotations', label: 'Quotations', icon: <RequestQuoteOutlined />, anyOf: [P.ManageQuotations, P.ViewBookings] },
      { to: '/trips', label: 'Trips', icon: <RouteOutlined />, anyOf: [P.ViewTrips] },
    ],
  },
  {
    title: 'Partners',
    items: [
      { to: '/customers', label: 'Customers', icon: <PeopleOutline />, anyOf: [P.ViewCustomers] },
      { to: '/owners', label: 'Vehicle owners', icon: <StoreOutlined />, anyOf: [P.ViewOwners] },
      { to: '/vehicles', label: 'Vehicles', icon: <LocalShippingOutlined />, anyOf: [P.ViewVehicles] },
      { to: '/drivers', label: 'Drivers', icon: <BadgeOutlined />, anyOf: [P.ViewDrivers] },
    ],
  },
  {
    title: 'Finance',
    items: [
      { to: '/invoices', label: 'Invoices', icon: <ReceiptLongOutlined />, anyOf: [P.ViewFinance] },
      { to: '/payments', label: 'Payments', icon: <PaymentsOutlined />, anyOf: [P.ViewFinance] },
      { to: '/settlements', label: 'Settlements', icon: <AccountBalanceOutlined />, anyOf: [P.ViewFinance] },
      { to: '/pricing', label: 'Pricing', icon: <PriceChangeOutlined />, anyOf: [P.ManagePricing] },
    ],
  },
  {
    title: 'Support',
    items: [
      { to: '/support/tickets', label: 'Tickets', icon: <SupportAgentOutlined />, anyOf: [P.ManageSupport] },
      { to: '/support/complaints', label: 'Complaints', icon: <ReportProblemOutlined />, anyOf: [P.ManageComplaints] },
      { to: '/support/enquiries', label: 'Website enquiries', icon: <MailOutline />, anyOf: [P.ManageSupport] },
    ],
  },
  {
    title: 'Insight & admin',
    items: [
      { to: '/reports', label: 'Reports', icon: <BarChartOutlined />, anyOf: [P.ViewReports] },
      { to: '/users', label: 'Staff users', icon: <ManageAccountsOutlined />, anyOf: [P.ManageUsers] },
      { to: '/roles', label: 'Roles & permissions', icon: <AdminPanelSettingsOutlined />, anyOf: [P.ManageRoles] },
      { to: '/settings', label: 'Master data & settings', icon: <TuneOutlined />, anyOf: [P.ManageMasterData, P.ManageSystemSettings, P.ManageNotificationTemplates] },
      { to: '/audit', label: 'Audit log', icon: <HistoryOutlined />, anyOf: [P.ViewAuditLogs] },
    ],
  },
];

export function Wordmark() {
  return (
    <Stack direction="row" spacing={1} sx={{ alignItems: 'center', color: '#fff' }}>
      <Box component="img" src="/favicon.svg" alt="" sx={{ width: 28, height: 28 }} />
      <Box>
        <Typography sx={{ fontWeight: 800, fontStretch: '75%', fontSize: '1.3rem', lineHeight: 1 }}>ProCargo</Typography>
        <Typography sx={{ fontSize: '0.75rem', color: '#AEB6BC' }}>Operations</Typography>
      </Box>
    </Stack>
  );
}

/**
 * Dark asphalt sidebar (the operations desk is used all day; the dark rail keeps attention on the work area) with the
 * sections each staff member's permissions allow.
 */
export function StaffLayout() {
  const { user, can, signOut } = useAuth();
  const theme = useTheme();
  const wide = useMediaQuery(theme.breakpoints.up('lg'));
  const [drawer, setDrawer] = useState(false);
  const [menu, setMenu] = useState<HTMLElement | null>(null);
  const location = useLocation();
  const navigate = useNavigate();
  const unread = useQuery({ queryKey: ['notifications', 'unread'], queryFn: notificationApi.unreadCount, refetchInterval: 60_000, enabled: !!user });

  const visible = sections
    .map((s) => ({ ...s, items: s.items.filter((i) => !i.anyOf || can(...i.anyOf)) }))
    .filter((s) => s.items.length > 0);
  const allTo = visible.flatMap((s) => s.items.map((i) => i.to));
  const activeTo = allTo
    .filter((to) => (to === '/' ? location.pathname === '/' : location.pathname === to || location.pathname.startsWith(`${to}/`)))
    .sort((a, b) => b.length - a.length)[0];

  const nav = (
    <Box sx={{ width: 244, bgcolor: palette.asphalt, color: '#E6E9EB', minHeight: '100%', pb: 3 }}>
      <Box sx={{ px: 2.5, py: 2.25 }}>
        <Wordmark />
      </Box>
      {visible.map((section) => (
        <List
          key={section.title}
          dense
          onClick={() => setDrawer(false)}
          subheader={
            <ListSubheader disableSticky sx={{ bgcolor: 'transparent', color: '#8D979E', fontWeight: 600, lineHeight: '32px' }}>
              {section.title}
            </ListSubheader>
          }
          sx={{ px: 1 }}
        >
          {section.items.map((item) => (
            <ListItemButton
              key={item.to}
              component={RouterLink}
              to={item.to}
              selected={item.to === activeTo}
              sx={{
                borderRadius: 1,
                color: '#E6E9EB',
                '&.Mui-selected': { bgcolor: 'rgba(245,197,24,.14)', color: palette.plate },
                '&.Mui-selected:hover, &:hover': { bgcolor: 'rgba(255,255,255,.06)' },
              }}
            >
              <ListItemIcon sx={{ minWidth: 34, color: 'inherit' }}>{item.icon}</ListItemIcon>
              <ListItemText primary={item.label} />
            </ListItemButton>
          ))}
        </List>
      ))}
    </Box>
  );

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh' }}>
      {wide ? (
        <Box component="nav" aria-label="Main" sx={{ flexShrink: 0, bgcolor: palette.asphalt }}>
          <Box sx={{ position: 'sticky', top: 0, maxHeight: '100vh', overflowY: 'auto' }}>{nav}</Box>
        </Box>
      ) : (
        <Drawer open={drawer} onClose={() => setDrawer(false)} slotProps={{ paper: { sx: { bgcolor: palette.asphalt } } }}>
          {nav}
        </Drawer>
      )}
      <Box sx={{ flex: 1, minWidth: 0 }}>
        <AppBar position="sticky" sx={{ borderBottom: 1, borderColor: 'divider', bgcolor: 'background.paper' }}>
          <Toolbar variant="dense" sx={{ gap: 1, minHeight: 56 }}>
            {!wide && (
              <IconButton onClick={() => setDrawer(true)} aria-label="Open menu" edge="start">
                <MenuIcon />
              </IconButton>
            )}
            <Box sx={{ flex: 1 }} />
            <IconButton component={RouterLink} to="/notifications" aria-label={`Notifications, ${unread.data?.count ?? 0} unread`}>
              <Badge badgeContent={unread.data?.count ?? 0} color="error">
                <NotificationsOutlined />
              </Badge>
            </IconButton>
            <Button onClick={(e) => setMenu(e.currentTarget)} color="inherit" sx={{ fontWeight: 600 }}>
              {user?.fullName}
            </Button>
            <Menu anchorEl={menu} open={!!menu} onClose={() => setMenu(null)}>
              <MenuItem disabled>{user?.roles.join(', ')}</MenuItem>
              <MenuItem
                onClick={() => {
                  setMenu(null);
                  navigate('/account/password');
                }}
              >
                Change password
              </MenuItem>
              <MenuItem
                onClick={async () => {
                  setMenu(null);
                  await signOut();
                  navigate('/login');
                }}
              >
                Sign out
              </MenuItem>
            </Menu>
          </Toolbar>
        </AppBar>
        <Container maxWidth="xl" sx={{ py: { xs: 2.5, md: 3.5 } }}>
          <Outlet />
        </Container>
      </Box>
    </Box>
  );
}
