import { useState, type ReactNode } from 'react';
import { Link as RouterLink, NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import {
  AppBar,
  Badge,
  Box,
  Button,
  Container,
  Divider,
  Drawer,
  IconButton,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
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
import DashboardOutlined from '@mui/icons-material/SpaceDashboardOutlined';
import Inventory2Outlined from '@mui/icons-material/Inventory2Outlined';
import ReceiptLongOutlined from '@mui/icons-material/ReceiptLongOutlined';
import SupportAgentOutlined from '@mui/icons-material/SupportAgentOutlined';
import PersonOutline from '@mui/icons-material/PersonOutline';
import LocalShippingOutlined from '@mui/icons-material/LocalShippingOutlined';
import BadgeOutlined from '@mui/icons-material/BadgeOutlined';
import RouteOutlined from '@mui/icons-material/RouteOutlined';
import AccountBalanceWalletOutlined from '@mui/icons-material/AccountBalanceWalletOutlined';
import AddCircleOutline from '@mui/icons-material/AddCircleOutline';
import { useQuery } from '@tanstack/react-query';
import { notificationApi } from '../api/endpoints';
import { useAuth, type PartnerRole } from '../auth/AuthContext';
import { palette } from '../theme';

export function Wordmark({ light }: { light?: boolean }) {
  return (
    <Stack direction="row" spacing={1} sx={{ alignItems: 'center', color: light ? '#fff' : palette.asphalt, textDecoration: 'none' }}>
      <Box component="img" src="/favicon.svg" alt="" sx={{ width: 30, height: 30 }} />
      <Typography sx={{ fontWeight: 800, fontStretch: '75%', fontSize: '1.45rem', letterSpacing: '-0.01em' }}>ProCargo</Typography>
    </Stack>
  );
}

// ---------------- public website ----------------

const publicLinks = [
  { to: '/', label: 'Book a lorry' },
  { to: '/partners', label: 'Truck owners & drivers' },
  { to: '/contact', label: 'Contact' },
];

export function PublicLayout() {
  const { user } = useAuth();
  const [open, setOpen] = useState(false);
  const theme = useTheme();
  const wide = useMediaQuery(theme.breakpoints.up('md'));

  return (
    <Box sx={{ minHeight: '100vh', display: 'flex', flexDirection: 'column' }}>
      <AppBar position="sticky" sx={{ borderBottom: 1, borderColor: 'divider', bgcolor: 'background.paper' }}>
        <Container maxWidth="lg">
          <Toolbar disableGutters sx={{ gap: 2 }}>
            <Box component={RouterLink} to="/" sx={{ textDecoration: 'none', mr: 'auto' }} aria-label="ProCargo home">
              <Wordmark />
            </Box>
            {wide ? (
              <>
                {publicLinks.map((l) => (
                  <Button key={l.to} component={NavLink} to={l.to} color="inherit" end>
                    {l.label}
                  </Button>
                ))}
                {user ? (
                  <Button component={RouterLink} to="/app" variant="contained">
                    My account
                  </Button>
                ) : (
                  <>
                    <Button component={RouterLink} to="/login" color="inherit">
                      Sign in
                    </Button>
                    <Button component={RouterLink} to="/register" variant="contained">
                      Create account
                    </Button>
                  </>
                )}
              </>
            ) : (
              <IconButton onClick={() => setOpen(true)} aria-label="Open menu">
                <MenuIcon />
              </IconButton>
            )}
          </Toolbar>
        </Container>
      </AppBar>
      <Drawer anchor="right" open={open} onClose={() => setOpen(false)}>
        <List sx={{ width: 260, p: 1 }} onClick={() => setOpen(false)}>
          {publicLinks.map((l) => (
            <ListItemButton key={l.to} component={RouterLink} to={l.to}>
              <ListItemText primary={l.label} />
            </ListItemButton>
          ))}
          <Divider sx={{ my: 1 }} />
          {user ? (
            <ListItemButton component={RouterLink} to="/app">
              <ListItemText primary="My account" />
            </ListItemButton>
          ) : (
            <>
              <ListItemButton component={RouterLink} to="/login">
                <ListItemText primary="Sign in" />
              </ListItemButton>
              <ListItemButton component={RouterLink} to="/register">
                <ListItemText primary="Create account" />
              </ListItemButton>
            </>
          )}
        </List>
      </Drawer>
      <Box component="main" sx={{ flex: 1 }}>
        <Outlet />
      </Box>
      <Box component="footer" sx={{ bgcolor: palette.asphalt, color: '#C9CED2', py: 5, mt: 8 }}>
        <Container maxWidth="lg">
          <Stack direction={{ xs: 'column', md: 'row' }} spacing={4} sx={{ justifyContent: 'space-between' }}>
            <Box sx={{ maxWidth: 360 }}>
              <Wordmark light />
              <Typography variant="body2" sx={{ mt: 1.5 }}>
                Lorries for businesses across Karnataka and South India. Verified owners, OTP-confirmed handover, GST invoices.
              </Typography>
            </Box>
            <Stack spacing={1}>
              {publicLinks.map((l) => (
                <Typography key={l.to} component={RouterLink} to={l.to} variant="body2" sx={{ color: 'inherit' }}>
                  {l.label}
                </Typography>
              ))}
            </Stack>
            <Typography variant="body2">
              ProCargo Logistics
              <br />
              Hubballi, Karnataka
              <br />
              support@procargo.com
            </Typography>
          </Stack>
        </Container>
      </Box>
    </Box>
  );
}

// ---------------- signed-in portals ----------------

interface NavItem {
  to: string;
  label: string;
  icon: ReactNode;
}

const navByRole: Record<PartnerRole, NavItem[]> = {
  customer: [
    { to: '/customer', label: 'Overview', icon: <DashboardOutlined /> },
    { to: '/customer/bookings/new', label: 'Book a lorry', icon: <AddCircleOutline /> },
    { to: '/customer/bookings', label: 'My bookings', icon: <Inventory2Outlined /> },
    { to: '/customer/invoices', label: 'Invoices & payments', icon: <ReceiptLongOutlined /> },
    { to: '/support', label: 'Help & complaints', icon: <SupportAgentOutlined /> },
    { to: '/customer/profile', label: 'Profile & addresses', icon: <PersonOutline /> },
  ],
  owner: [
    { to: '/owner', label: 'Overview', icon: <DashboardOutlined /> },
    { to: '/owner/vehicles', label: 'Vehicles', icon: <LocalShippingOutlined /> },
    { to: '/owner/drivers', label: 'Drivers', icon: <BadgeOutlined /> },
    { to: '/owner/trips', label: 'Trips', icon: <RouteOutlined /> },
    { to: '/owner/earnings', label: 'Earnings', icon: <AccountBalanceWalletOutlined /> },
    { to: '/support', label: 'Help & complaints', icon: <SupportAgentOutlined /> },
    { to: '/owner/profile', label: 'Business profile', icon: <PersonOutline /> },
  ],
  driver: [
    { to: '/driver', label: 'My trips', icon: <RouteOutlined /> },
    { to: '/support', label: 'Help', icon: <SupportAgentOutlined /> },
    { to: '/driver/profile', label: 'My profile', icon: <PersonOutline /> },
  ],
};

const roleTitle: Record<PartnerRole, string> = { customer: 'Customer', owner: 'Truck owner', driver: 'Driver' };

export function PortalLayout() {
  const { user, role, signOut } = useAuth();
  const theme = useTheme();
  const wide = useMediaQuery(theme.breakpoints.up('md'));
  const [drawer, setDrawer] = useState(false);
  const [menu, setMenu] = useState<HTMLElement | null>(null);
  const navigate = useNavigate();
  const location = useLocation();

  const unread = useQuery({ queryKey: ['notifications', 'unread'], queryFn: notificationApi.unreadCount, refetchInterval: 60_000, enabled: !!user });
  const items = role ? navByRole[role] : [];

  const activeTo = items
    .map((i) => i.to)
    .filter((to) => location.pathname === to || location.pathname.startsWith(`${to}/`))
    .sort((x, y) => y.length - x.length)[0];
  const isActive = (to: string) => to === activeTo;

  const nav = (
    <Box sx={{ p: 1.5, width: 248 }}>
      <Box component={RouterLink} to="/" sx={{ display: 'block', textDecoration: 'none', px: 1, py: 1.5 }}>
        <Wordmark />
      </Box>
      <Typography variant="body2" color="text.secondary" sx={{ px: 1, mb: 1 }}>
        {role ? roleTitle[role] : ''} account
      </Typography>
      <List dense onClick={() => setDrawer(false)}>
        {items.map((item) => (
          <ListItemButton key={item.to} component={RouterLink} to={item.to} selected={isActive(item.to)} sx={{ mb: 0.25 }}>
            <ListItemIcon sx={{ minWidth: 36, color: 'inherit' }}>{item.icon}</ListItemIcon>
            <ListItemText primary={item.label} slotProps={{ primary: { fontWeight: 'inherit' } }} />
          </ListItemButton>
        ))}
      </List>
    </Box>
  );

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh' }}>
      {wide ? (
        <Box component="nav" aria-label="Main" sx={{ borderRight: 1, borderColor: 'divider', bgcolor: 'background.paper', flexShrink: 0 }}>
          <Box sx={{ position: 'sticky', top: 0 }}>{nav}</Box>
        </Box>
      ) : (
        <Drawer open={drawer} onClose={() => setDrawer(false)}>
          {nav}
        </Drawer>
      )}
      <Box sx={{ flex: 1, minWidth: 0 }}>
        <AppBar position="sticky" sx={{ borderBottom: 1, borderColor: 'divider', bgcolor: 'background.paper' }}>
          <Toolbar sx={{ gap: 1 }}>
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
              {user?.fullName.split(' ')[0]}
            </Button>
            <Menu anchorEl={menu} open={!!menu} onClose={() => setMenu(null)}>
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
                  navigate('/');
                }}
              >
                Sign out
              </MenuItem>
            </Menu>
          </Toolbar>
        </AppBar>
        <Container maxWidth="lg" sx={{ py: { xs: 2.5, md: 4 } }}>
          <Outlet />
        </Container>
      </Box>
    </Box>
  );
}
