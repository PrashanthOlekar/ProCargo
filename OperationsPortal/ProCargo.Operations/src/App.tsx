import { Navigate, Route, Routes } from 'react-router-dom';
import { P, RequireStaff } from './auth/AuthContext';
import { StaffLayout } from './components/Shell';
import { ChangePasswordPage, DashboardPage, ForgotPasswordPage, LoginPage, NotificationsPage, ResetPasswordPage } from './pages/HomePages';
import { BookingDetailPage, BookingsPage, QuotationDetailPage, QuotationsPage } from './pages/BookingPages';
import { TripDetailPage, TripsPage } from './pages/TripPages';
import {
  CustomerDetailPage,
  CustomersPage,
  DriverDetailPage,
  DriversPage,
  OwnerDetailPage,
  OwnersPage,
  VehicleDetailPage,
  VehiclesPage,
} from './pages/PartnerPages';
import { InvoiceDetailPage, InvoicesPage, PaymentDetailPage, PaymentsPage, SettlementDetailPage, SettlementsPage } from './pages/FinancePages';
import { ComplaintDetailPage, ComplaintsPage, EnquiriesPage, TicketDetailPage, TicketsPage } from './pages/SupportPages';
import { ReportsPage } from './pages/ReportsPage';
import { AuditLogPage, RolesPage, UserDetailPage, UsersPage } from './pages/AdminPages';
import { PricingPage, SettingsPage } from './pages/ConfigPages';

/**
 * Operations portal routes. Every staff route sits behind RequireStaff; sections additionally require one of the
 * permissions that unlock them. The menu hides what a role cannot use, and the API checks every call regardless.
 */
export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/forgot-password" element={<ForgotPasswordPage />} />
      <Route path="/reset-password" element={<ResetPasswordPage />} />

      <Route element={<RequireStaff />}>
        <Route element={<StaffLayout />}>
          <Route index element={<DashboardPage />} />
          <Route path="notifications" element={<NotificationsPage />} />
          <Route path="account/password" element={<ChangePasswordPage />} />

          <Route element={<RequireStaff anyOf={[P.ViewBookings]} />}>
            <Route path="bookings" element={<BookingsPage />} />
            <Route path="bookings/:id" element={<BookingDetailPage />} />
          </Route>
          <Route element={<RequireStaff anyOf={[P.ManageQuotations, P.ViewBookings]} />}>
            <Route path="quotations" element={<QuotationsPage />} />
            <Route path="quotations/:id" element={<QuotationDetailPage />} />
          </Route>
          <Route element={<RequireStaff anyOf={[P.ViewTrips]} />}>
            <Route path="trips" element={<TripsPage />} />
            <Route path="trips/:id" element={<TripDetailPage />} />
          </Route>

          <Route element={<RequireStaff anyOf={[P.ViewCustomers]} />}>
            <Route path="customers" element={<CustomersPage />} />
            <Route path="customers/:id" element={<CustomerDetailPage />} />
          </Route>
          <Route element={<RequireStaff anyOf={[P.ViewOwners]} />}>
            <Route path="owners" element={<OwnersPage />} />
            <Route path="owners/:id" element={<OwnerDetailPage />} />
          </Route>
          <Route element={<RequireStaff anyOf={[P.ViewVehicles]} />}>
            <Route path="vehicles" element={<VehiclesPage />} />
            <Route path="vehicles/:id" element={<VehicleDetailPage />} />
          </Route>
          <Route element={<RequireStaff anyOf={[P.ViewDrivers]} />}>
            <Route path="drivers" element={<DriversPage />} />
            <Route path="drivers/:id" element={<DriverDetailPage />} />
          </Route>

          <Route element={<RequireStaff anyOf={[P.ViewFinance]} />}>
            <Route path="invoices" element={<InvoicesPage />} />
            <Route path="invoices/:id" element={<InvoiceDetailPage />} />
            <Route path="payments" element={<PaymentsPage />} />
            <Route path="payments/:id" element={<PaymentDetailPage />} />
            <Route path="settlements" element={<SettlementsPage />} />
            <Route path="settlements/:id" element={<SettlementDetailPage />} />
          </Route>
          <Route element={<RequireStaff anyOf={[P.ManagePricing]} />}>
            <Route path="pricing" element={<PricingPage />} />
          </Route>

          <Route element={<RequireStaff anyOf={[P.ManageSupport]} />}>
            <Route path="support/tickets" element={<TicketsPage />} />
            <Route path="support/tickets/:id" element={<TicketDetailPage />} />
            <Route path="support/enquiries" element={<EnquiriesPage />} />
          </Route>
          <Route element={<RequireStaff anyOf={[P.ManageComplaints]} />}>
            <Route path="support/complaints" element={<ComplaintsPage />} />
            <Route path="support/complaints/:id" element={<ComplaintDetailPage />} />
          </Route>

          <Route element={<RequireStaff anyOf={[P.ViewReports]} />}>
            <Route path="reports" element={<ReportsPage />} />
          </Route>
          <Route element={<RequireStaff anyOf={[P.ManageUsers]} />}>
            <Route path="users" element={<UsersPage />} />
            <Route path="users/:id" element={<UserDetailPage />} />
          </Route>
          <Route element={<RequireStaff anyOf={[P.ManageRoles]} />}>
            <Route path="roles" element={<RolesPage />} />
          </Route>
          <Route element={<RequireStaff anyOf={[P.ManageMasterData, P.ManageSystemSettings, P.ManageNotificationTemplates]} />}>
            <Route path="settings" element={<SettingsPage />} />
          </Route>
          <Route element={<RequireStaff anyOf={[P.ViewAuditLogs]} />}>
            <Route path="audit" element={<AuditLogPage />} />
          </Route>
        </Route>
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
