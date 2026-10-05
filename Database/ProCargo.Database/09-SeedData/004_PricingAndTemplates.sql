/*
    Starting pricing configuration and notification templates.
    Values are business defaults - Finance edits them in Operations portal > Pricing.
    Template placeholders use {{Name}} syntax and are HTML-encoded by the API before e-mail rendering.
*/

;WITH src AS (SELECT * FROM (VALUES
    ('MINI_TRUCK',      300.00,  500.00, 18.00),
    ('PICKUP_1_5T',     400.00,  700.00, 22.00),
    ('LCV_14FT',        800.00, 1500.00, 32.00),
    ('TRUCK_17FT',     1000.00, 1800.00, 36.00),
    ('TRUCK_19FT',     1200.00, 2200.00, 40.00),
    ('CONTAINER_20FT', 1500.00, 2500.00, 45.00),
    ('TRUCK_22FT',     1800.00, 3000.00, 50.00),
    ('CONTAINER_32SXL',2500.00, 4500.00, 60.00),
    ('CONTAINER_32MXL',3000.00, 5500.00, 70.00),
    ('TRAILER_40FT',   5000.00, 9000.00, 95.00)
) AS v(Code, BaseFare, MinimumFare, PerKmRate))
INSERT INTO fin.VehiclePricing (VehicleTypeId, BaseFare, MinimumFare, PerKmRate, PerKgRate, FreeWaitingHours, EffectiveFromUtc)
SELECT vt.VehicleTypeId, s.BaseFare, s.MinimumFare, s.PerKmRate, 0, 2, '2026-01-01'
FROM src AS s
INNER JOIN mst.VehicleType AS vt ON vt.Code = s.Code
WHERE NOT EXISTS (SELECT 1 FROM fin.VehiclePricing AS x WHERE x.VehicleTypeId = vt.VehicleTypeId);
GO

-- Tapered per-km slabs: 100% up to 100 km, 90% for 100-500 km, 80% beyond 500 km.
INSERT INTO fin.DistancePricing (VehicleTypeId, FromKm, ToKm, RatePerKm)
SELECT vp.VehicleTypeId, slab.FromKm, slab.ToKm, CAST(ROUND(vp.PerKmRate * slab.Factor, 2) AS DECIMAL(10,2))
FROM fin.VehiclePricing AS vp
CROSS JOIN (VALUES (CAST(0 AS DECIMAL(8,2)), CAST(100 AS DECIMAL(8,2)), CAST(1.00 AS DECIMAL(4,2))),
                   (100, 500, 0.90),
                   (500, NULL, 0.80)) AS slab(FromKm, ToKm, Factor)
WHERE vp.IsActive = 1
  AND NOT EXISTS (SELECT 1 FROM fin.DistancePricing AS x WHERE x.VehicleTypeId = vp.VehicleTypeId);
GO

;WITH src AS (SELECT * FROM (VALUES
    ('LOADING',          N'Loading charge',            'Flat',       300.00),
    ('UNLOADING',        N'Unloading charge',          'Flat',       300.00),
    ('WAITING',          N'Waiting charge per hour',   'PerHour',    200.00),
    ('NIGHT',            N'Night movement surcharge',  'Percentage',  10.00),
    ('SPECIAL_HANDLING', N'Special handling surcharge','Percentage',  15.00)
) AS v(ChargeCode, Name, CalculationType, Amount))
INSERT INTO fin.AdditionalCharge (ChargeCode, Name, VehicleTypeId, CalculationType, Amount)
SELECT s.ChargeCode, s.Name, NULL, s.CalculationType, s.Amount FROM src AS s
WHERE NOT EXISTS (SELECT 1 FROM fin.AdditionalCharge AS x WHERE x.ChargeCode = s.ChargeCode AND x.VehicleTypeId IS NULL);
GO

IF NOT EXISTS (SELECT 1 FROM fin.PricingRule WHERE Name = N'Karnataka launch offer')
    INSERT INTO fin.PricingRule (Name, StateId, AdjustmentType, AdjustmentValue, Priority, EffectiveFromUtc, EffectiveToUtc)
    SELECT N'Karnataka launch offer', s.StateId, 'Percentage', -5.00, 100, '2026-01-01', '2027-01-01'
    FROM mst.State AS s WHERE s.StateCode = 'KA';
GO

IF NOT EXISTS (SELECT 1 FROM fin.TaxRate WHERE Code = 'GST_GTA')
    INSERT INTO fin.TaxRate (Code, Name, RatePercent, EffectiveFromUtc)
    VALUES ('GST_GTA', N'GST on goods transport', 5.00, '2017-07-01');
GO

IF NOT EXISTS (SELECT 1 FROM fin.CommissionRule WHERE VehicleTypeId IS NULL)
    INSERT INTO fin.CommissionRule (Name, VehicleTypeId, CommissionPercent, MinimumCommission, EffectiveFromUtc)
    VALUES (N'Default platform commission', NULL, 10.00, 100.00, '2026-01-01');
GO

;WITH src AS (SELECT * FROM (VALUES
    ('WELCOME',              1, N'Welcome to ProCargo',             N'Hi {{Name}}, your ProCargo account is ready.'),
    ('WELCOME',              2, N'Welcome to ProCargo',             N'Hi {{Name}},<br/>Welcome to ProCargo Logistics. You can now sign in and start using the platform.'),
    ('PASSWORD_RESET',       2, N'Reset your ProCargo password',    N'Hi {{Name}},<br/>Use the link below to reset your password. It expires in {{ExpiryMinutes}} minutes.<br/><a href="{{ResetLink}}">Reset password</a><br/>If you did not request this, ignore this e-mail.'),
    ('ACCOUNT_INVITE',       2, N'You have been invited to ProCargo',N'Hi {{Name}},<br/>An account has been created for you on ProCargo. Set your password using the link below (valid for {{ExpiryMinutes}} minutes).<br/><a href="{{ResetLink}}">Set password</a>'),
    ('BOOKING_SUBMITTED',    1, N'Booking submitted',               N'Your booking {{BookingNumber}} has been submitted. We will share a quotation shortly.'),
    ('BOOKING_CANCELLED',    1, N'Booking cancelled',               N'Booking {{BookingNumber}} has been cancelled.'),
    ('BOOKING_REJECTED',     1, N'Booking rejected',                N'Booking {{BookingNumber}} could not be accepted: {{Reason}}'),
    ('QUOTATION_SENT',       1, N'Quotation ready',                 N'Quotation {{QuotationNumber}} for booking {{BookingNumber}} is ready: INR {{TotalAmount}}. Valid until {{ValidUntil}}.'),
    ('QUOTATION_SENT',       2, N'Your ProCargo quotation {{QuotationNumber}}', N'Hi {{Name}},<br/>Your quotation for booking {{BookingNumber}} is INR {{TotalAmount}} (valid until {{ValidUntil}}). Sign in to accept or reject it.'),
    ('TRIP_ASSIGNED',        1, N'Vehicle assigned',                N'Trip {{TripNumber}} for booking {{BookingNumber}}: vehicle {{VehicleNumber}}, driver {{DriverName}}.'),
    ('TRIP_ASSIGNED_DRIVER', 1, N'New trip assigned',               N'You have been assigned trip {{TripNumber}}. Pickup planned at {{PickupTime}}.'),
    ('TRIP_ASSIGNED_OWNER',  1, N'Trip assigned to your vehicle',   N'Trip {{TripNumber}} has been assigned to vehicle {{VehicleNumber}}.'),
    ('PICKUP_OTP',           3, N'ProCargo pickup OTP',             N'ProCargo: OTP {{Otp}} to confirm pickup for trip {{TripNumber}}. Share it with the driver only at pickup. Valid {{ValidityMinutes}} min.'),
    ('DELIVERY_OTP',         3, N'ProCargo delivery OTP',           N'ProCargo: OTP {{Otp}} to confirm delivery for trip {{TripNumber}}. Share it with the driver only after receiving goods. Valid {{ValidityMinutes}} min.'),
    ('TRIP_STARTED',         1, N'Shipment in transit',             N'Your shipment {{BookingNumber}} is on the way.'),
    ('TRIP_DELIVERED',       1, N'Shipment delivered',              N'Your shipment {{BookingNumber}} has been delivered.'),
    ('INVOICE_ISSUED',       1, N'Invoice issued',                  N'Invoice {{InvoiceNumber}} for INR {{TotalAmount}} is due on {{DueDate}}.'),
    ('INVOICE_ISSUED',       2, N'ProCargo invoice {{InvoiceNumber}}', N'Hi {{Name}},<br/>Invoice {{InvoiceNumber}} for INR {{TotalAmount}} is due on {{DueDate}}. Sign in to pay online.'),
    ('PAYMENT_RECEIVED',     1, N'Payment received',                N'We received INR {{Amount}} against invoice {{InvoiceNumber}}. Thank you.'),
    ('SETTLEMENT_COMPLETED', 1, N'Settlement paid',                 N'Settlement {{SettlementNumber}} of INR {{NetAmount}} has been paid. Reference {{TransactionReference}}.'),
    ('VERIFICATION_UPDATED', 1, N'Verification update',             N'Your {{Subject}} verification status is now {{Status}}. {{Remarks}}'),
    ('TICKET_UPDATED',       1, N'Support ticket update',           N'Ticket {{TicketNumber}} is now {{Status}}.'),
    ('COMPLAINT_UPDATED',    1, N'Complaint update',                N'Complaint {{ComplaintNumber}} is now {{Status}}.')
) AS v(TemplateCode, NotificationChannelId, Subject, Body))
INSERT INTO sup.NotificationTemplate (TemplateCode, NotificationChannelId, Subject, Body)
SELECT s.TemplateCode, s.NotificationChannelId, s.Subject, s.Body FROM src AS s
WHERE NOT EXISTS (SELECT 1 FROM sup.NotificationTemplate AS x WHERE x.TemplateCode = s.TemplateCode AND x.NotificationChannelId = s.NotificationChannelId);
GO
