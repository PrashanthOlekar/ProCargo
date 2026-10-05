/*
    Roles, permissions and the default role -> permission matrix.
    Permission codes MUST match ProCargo.Domain/Constants/Permissions.cs.
    New roles can be created at runtime (Operations portal > Administration > Roles); system roles cannot be deleted.
*/

MERGE sec.Permission AS target
USING (VALUES
    -- internal (staff) permissions: only usable from the Operations portal
    ('AccessOperationsPortal',     N'Sign in to the Operations portal',          'Security',     1),
    ('ManageUsers',                N'Create and manage internal users',          'Security',     1),
    ('ManageRoles',                N'Manage roles and permissions',              'Security',     1),
    ('ViewCustomers',              N'View customers',                            'Customers',    1),
    ('ManageCustomers',            N'Manage customers',                          'Customers',    1),
    ('ViewOwners',                 N'View vehicle owners',                       'Owners',       1),
    ('ManageOwners',               N'Manage vehicle owners',                     'Owners',       1),
    ('ApproveOwners',              N'Verify / approve vehicle owners',           'Owners',       1),
    ('ViewDrivers',                N'View drivers',                              'Drivers',      1),
    ('ManageDrivers',              N'Manage drivers',                            'Drivers',      1),
    ('ApproveDrivers',             N'Verify / approve drivers',                  'Drivers',      1),
    ('ViewVehicles',               N'View vehicles',                             'Vehicles',     1),
    ('ManageVehicles',             N'Manage vehicles',                           'Vehicles',     1),
    ('ApproveVehicles',            N'Verify / approve vehicles',                 'Vehicles',     1),
    ('ViewBookings',               N'View all bookings',                         'Bookings',     1),
    ('ManageBookings',             N'Review, hold, reject and manage bookings',  'Bookings',     1),
    ('ManageQuotations',           N'Create, edit, send and withdraw quotations','Quotations',   1),
    ('ApproveQuotations',          N'Approve quotations above discount limit',   'Quotations',   1),
    ('ViewTrips',                  N'View all trips',                            'Trips',        1),
    ('AssignTrips',                N'Create trips and assign vehicles/drivers',  'Trips',        1),
    ('UpdateTrips',                N'Update trip status / handle exceptions',    'Trips',        1),
    ('ManagePricing',              N'Manage pricing configuration',              'Pricing',      1),
    ('ViewFinance',                N'View invoices, payments and settlements',   'Finance',      1),
    ('ManageInvoices',             N'Generate and adjust invoices',              'Finance',      1),
    ('ManagePayments',             N'Record and reconcile payments',             'Finance',      1),
    ('ManageRefunds',              N'Issue refunds',                             'Finance',      1),
    ('ManageSettlements',          N'Create and process owner settlements',      'Finance',      1),
    ('ApproveSettlements',         N'Approve owner settlements',                 'Finance',      1),
    ('ViewReports',                N'View dashboards and reports',               'Reports',      1),
    ('ManageSupport',              N'Handle support tickets',                    'Support',      1),
    ('ManageComplaints',           N'Handle complaints',                         'Support',      1),
    ('ManageMasterData',           N'Manage master data',                        'Administration', 1),
    ('ManageSystemSettings',       N'Manage system settings',                    'Administration', 1),
    ('ManageNotificationTemplates',N'Manage notification templates',            'Administration', 1),
    ('ViewAuditLogs',              N'View audit logs',                           'Administration', 1),
    -- external permissions: only usable from the customer-facing portal
    ('CreateBookings',             N'Create and manage own bookings',            'Bookings',     0),
    ('RespondToQuotations',        N'Accept or reject own quotations',           'Quotations',   0),
    ('MakePayments',               N'Pay own invoices',                          'Finance',      0),
    ('ManageOwnFleet',             N'Manage own vehicles and drivers',           'Owners',       0),
    ('PerformTrips',               N'Execute assigned trips',                    'Trips',        0),
    ('RaiseSupportRequests',       N'Raise complaints and support tickets',      'Support',      0)
) AS source (Code, Name, Module, IsInternal)
ON target.Code = source.Code
WHEN MATCHED THEN UPDATE SET Name = source.Name, Module = source.Module, IsInternal = source.IsInternal
WHEN NOT MATCHED BY TARGET THEN INSERT (Code, Name, Module, IsInternal) VALUES (source.Code, source.Name, source.Module, source.IsInternal);
GO

MERGE sec.Role AS target
USING (VALUES
    ('SuperAdmin',   N'Full access to the platform',                           1, 1),
    ('Admin',        N'Administers users, master data and all operations',    1, 1),
    ('Operations',   N'Runs bookings, quotations, trips and verification',    1, 1),
    ('Finance',      N'Invoices, payments, refunds, settlements and pricing', 1, 1),
    ('Support',      N'Support tickets and complaints',                       1, 1),
    ('Customer',     N'Books shipments',                                      1, 0),
    ('VehicleOwner', N'Owns vehicles and employs drivers',                    1, 0),
    ('Driver',       N'Executes trips',                                       1, 0)
) AS source (Name, Description, IsSystem, IsInternal)
ON target.Name = source.Name
WHEN MATCHED THEN UPDATE SET Description = source.Description, IsSystem = source.IsSystem, IsInternal = source.IsInternal
WHEN NOT MATCHED BY TARGET THEN INSERT (Name, Description, IsSystem, IsInternal) VALUES (source.Name, source.Description, source.IsSystem, source.IsInternal);
GO

DECLARE @Matrix TABLE (RoleName VARCHAR(50), PermissionCode VARCHAR(80));

-- SuperAdmin: every internal permission
INSERT INTO @Matrix (RoleName, PermissionCode)
SELECT 'SuperAdmin', p.Code FROM sec.Permission AS p WHERE p.IsInternal = 1;

-- Admin: every internal permission except role and system-setting management
INSERT INTO @Matrix (RoleName, PermissionCode)
SELECT 'Admin', p.Code FROM sec.Permission AS p WHERE p.IsInternal = 1 AND p.Code NOT IN ('ManageRoles', 'ManageSystemSettings');

INSERT INTO @Matrix (RoleName, PermissionCode) VALUES
    ('Operations','AccessOperationsPortal'), ('Operations','ViewCustomers'), ('Operations','ViewOwners'), ('Operations','ManageOwners'),
    ('Operations','ApproveOwners'), ('Operations','ViewDrivers'), ('Operations','ManageDrivers'), ('Operations','ApproveDrivers'),
    ('Operations','ViewVehicles'), ('Operations','ManageVehicles'), ('Operations','ApproveVehicles'), ('Operations','ViewBookings'),
    ('Operations','ManageBookings'), ('Operations','ManageQuotations'), ('Operations','ViewTrips'), ('Operations','AssignTrips'),
    ('Operations','UpdateTrips'), ('Operations','ViewReports'), ('Operations','ManageComplaints'),

    ('Finance','AccessOperationsPortal'), ('Finance','ViewCustomers'), ('Finance','ViewOwners'), ('Finance','ViewBookings'),
    ('Finance','ViewTrips'), ('Finance','ViewFinance'), ('Finance','ManageInvoices'), ('Finance','ManagePayments'),
    ('Finance','ManageRefunds'), ('Finance','ManageSettlements'), ('Finance','ApproveSettlements'), ('Finance','ApproveQuotations'),
    ('Finance','ManagePricing'), ('Finance','ViewReports'),

    ('Support','AccessOperationsPortal'), ('Support','ViewCustomers'), ('Support','ViewOwners'), ('Support','ViewDrivers'),
    ('Support','ViewVehicles'), ('Support','ViewBookings'), ('Support','ViewTrips'), ('Support','ViewFinance'),
    ('Support','ManageSupport'), ('Support','ManageComplaints'),

    ('Customer','CreateBookings'), ('Customer','RespondToQuotations'), ('Customer','MakePayments'), ('Customer','RaiseSupportRequests'),
    ('VehicleOwner','ManageOwnFleet'), ('VehicleOwner','RaiseSupportRequests'),
    ('Driver','PerformTrips'), ('Driver','RaiseSupportRequests');

INSERT INTO sec.RolePermission (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId
FROM @Matrix AS m
INNER JOIN sec.Role       AS r ON r.Name = m.RoleName
INNER JOIN sec.Permission AS p ON p.Code = m.PermissionCode
WHERE NOT EXISTS (SELECT 1 FROM sec.RolePermission AS rp WHERE rp.RoleId = r.RoleId AND rp.PermissionId = p.PermissionId);
GO
