import { Navigate, Route, Routes } from 'react-router-dom';
import { homePathFor, useAuth } from './auth/AuthContext';
import { FullPageSpinner, RequireRole, RequireSignedIn } from './auth/guards';
import { PortalLayout, PublicLayout } from './components/Shell';
import HomePage from './pages/public/HomePage';
import { ContactPage, ForgotPasswordPage, LoginPage, NotFoundPage, PartnersPage, RegisterPage, ResetPasswordPage } from './pages/public/PublicPages';
import { CustomerDashboardPage, InvoiceDetailPage, InvoicesPage } from './pages/customer/FinancePages';
import { BookingDetailPage, BookingsPage } from './pages/customer/BookingPages';
import { EditBookingPage, NewBookingPage } from './pages/customer/BookingEditor';
import { CustomerProfilePage } from './pages/customer/ProfilePage';
import {
  DriverDetailPage,
  DriversPage,
  EarningsPage,
  OwnerDashboardPage,
  OwnerTripsPage,
  TripDetailReadOnly,
  VehicleDetailPage,
  VehiclesPage,
} from './pages/owner/OwnerPages';
import { OwnerProfilePage } from './pages/owner/OwnerProfilePage';
import { DriverHomePage, DriverProfilePage, DriverTripPage } from './pages/driver/DriverPages';
import { ChangePasswordPage, ComplaintDetailPage, NotificationsPage, SupportPage, TicketDetailPage } from './pages/account/AccountPages';

/** Sends a signed-in person to their own portal. */
function AppHome() {
  const { user, initializing } = useAuth();
  if (initializing) return <FullPageSpinner />;
  return <Navigate to={user ? homePathFor(user) : '/login'} replace />;
}

export default function App() {
  return (
    <Routes>
      <Route element={<PublicLayout />}>
        <Route index element={<HomePage />} />
        <Route path="partners" element={<PartnersPage />} />
        <Route path="contact" element={<ContactPage />} />
        <Route path="login" element={<LoginPage />} />
        <Route path="register" element={<RegisterPage />} />
        <Route path="forgot-password" element={<ForgotPasswordPage />} />
        <Route path="reset-password" element={<ResetPasswordPage />} />
        <Route path="app" element={<AppHome />} />
        <Route element={<RequireSignedIn />}>
          <Route path="account/password" element={<ChangePasswordPage />} />
        </Route>
      </Route>

      <Route element={<RequireRole role="customer" />}>
        <Route path="customer" element={<PortalLayout />}>
          <Route index element={<CustomerDashboardPage />} />
          <Route path="bookings" element={<BookingsPage />} />
          <Route path="bookings/new" element={<NewBookingPage />} />
          <Route path="bookings/:id" element={<BookingDetailPage />} />
          <Route path="bookings/:id/edit" element={<EditBookingPage />} />
          <Route path="invoices" element={<InvoicesPage />} />
          <Route path="invoices/:id" element={<InvoiceDetailPage />} />
          <Route path="profile" element={<CustomerProfilePage />} />
        </Route>
      </Route>

      <Route element={<RequireRole role="owner" />}>
        <Route path="owner" element={<PortalLayout />}>
          <Route index element={<OwnerDashboardPage />} />
          <Route path="vehicles" element={<VehiclesPage />} />
          <Route path="vehicles/:id" element={<VehicleDetailPage />} />
          <Route path="drivers" element={<DriversPage />} />
          <Route path="drivers/:id" element={<DriverDetailPage />} />
          <Route path="trips" element={<OwnerTripsPage />} />
          <Route path="trips/:id" element={<TripDetailReadOnly backPath="/owner/trips" />} />
          <Route path="earnings" element={<EarningsPage />} />
          <Route path="profile" element={<OwnerProfilePage />} />
        </Route>
      </Route>

      <Route element={<RequireRole role="driver" />}>
        <Route path="driver" element={<PortalLayout />}>
          <Route index element={<DriverHomePage />} />
          <Route path="trips/:id" element={<DriverTripPage />} />
          <Route path="profile" element={<DriverProfilePage />} />
        </Route>
      </Route>

      <Route element={<RequireSignedIn />}>
        <Route element={<PortalLayout />}>
          <Route path="notifications" element={<NotificationsPage />} />
          <Route path="support" element={<SupportPage />} />
          <Route path="support/tickets/:id" element={<TicketDetailPage />} />
          <Route path="support/complaints/:id" element={<ComplaintDetailPage />} />
        </Route>
      </Route>

      <Route element={<PublicLayout />}>
        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Routes>
  );
}
