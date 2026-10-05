/*
    DEVELOPMENT / TEST DATA ONLY - never run against staging or production.
    Every account below uses the password:  ProCargo@Dev1
    (ASP.NET Core Identity v3 hashes, PBKDF2-HMAC-SHA512, 100k iterations.)
    The SuperAdmin is NOT created here; the API creates it on first start from the
    Bootstrap:SuperAdmin* settings (user-secrets / environment variables).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF EXISTS (SELECT 1 FROM sec.[User] WHERE NormalizedEmail = N'OPS@PROCARGO.TEST')
BEGIN
    PRINT 'Test data already present - skipping.';
    RETURN;
END

BEGIN TRANSACTION;

DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
DECLARE @UserId BIGINT;

INSERT INTO sec.[User] (Email, NormalizedEmail, PhoneNumber, FullName, PasswordHash, IsInternal, EmailConfirmed)
VALUES (N'admin@procargo.test', N'ADMIN@PROCARGO.TEST', '+919900000001', N'Asha Admin', N'AQAAAAIAAYagAAAAEPl8QxzjG3NAMV98+zEreBua3BldwzeH88OdU3jP0VqWAZVEzQx7l6C/I9jTZOfMfA==', 1, 1);
SET @UserId = SCOPE_IDENTITY();
INSERT INTO sec.UserRole (UserId, RoleId) SELECT @UserId, RoleId FROM sec.Role WHERE Name = 'Admin';

INSERT INTO sec.[User] (Email, NormalizedEmail, PhoneNumber, FullName, PasswordHash, IsInternal, EmailConfirmed)
VALUES (N'ops@procargo.test', N'OPS@PROCARGO.TEST', '+919900000002', N'Omkar Operations', N'AQAAAAIAAYagAAAAEFCQi2rh1yQYUi+XHY/p+C08uaQh3HvDc5HX1YYWSMxXOAVRTw0h3mPkhMGmyuwFOw==', 1, 1);
SET @UserId = SCOPE_IDENTITY();
INSERT INTO sec.UserRole (UserId, RoleId) SELECT @UserId, RoleId FROM sec.Role WHERE Name = 'Operations';

INSERT INTO sec.[User] (Email, NormalizedEmail, PhoneNumber, FullName, PasswordHash, IsInternal, EmailConfirmed)
VALUES (N'finance@procargo.test', N'FINANCE@PROCARGO.TEST', '+919900000003', N'Farah Finance', N'AQAAAAIAAYagAAAAEL27efcMyhHxvcUJGGjX8YwIiQcTb14rP5knkllWK39VHKOPvfQ1TgP0yB+jM3pEyA==', 1, 1);
SET @UserId = SCOPE_IDENTITY();
INSERT INTO sec.UserRole (UserId, RoleId) SELECT @UserId, RoleId FROM sec.Role WHERE Name = 'Finance';

INSERT INTO sec.[User] (Email, NormalizedEmail, PhoneNumber, FullName, PasswordHash, IsInternal, EmailConfirmed)
VALUES (N'support@procargo.test', N'SUPPORT@PROCARGO.TEST', '+919900000004', N'Suresh Support', N'AQAAAAIAAYagAAAAEL5IWagRxj2XHPYG7WK0PLAH9RPvDZZIpsoBCchhyBaMt8+epOpHo++IPVPVztPlQQ==', 1, 1);
SET @UserId = SCOPE_IDENTITY();
INSERT INTO sec.UserRole (UserId, RoleId) SELECT @UserId, RoleId FROM sec.Role WHERE Name = 'Support';


-- Customer
DECLARE @CustomerUserId BIGINT, @CustomerId BIGINT;
INSERT INTO sec.[User] (Email, NormalizedEmail, PhoneNumber, FullName, PasswordHash, IsInternal, EmailConfirmed)
VALUES (N'customer@procargo.test', N'CUSTOMER@PROCARGO.TEST', '+919900000010', N'Chetan Traders', N'AQAAAAIAAYagAAAAEEqbMyfHwSyjcBIGCrTAJIez0Bj63aqmuN3dcOC/R5BhHusAHzRSso0RtANjeFXigw==', 0, 1);
SET @CustomerUserId = SCOPE_IDENTITY();
INSERT INTO sec.UserRole (UserId, RoleId) SELECT @CustomerUserId, RoleId FROM sec.Role WHERE Name = 'Customer';
INSERT INTO core.Customer (UserId, CustomerNumber, CustomerTypeId, FullName, CompanyName, Email, PhoneNumber, CreatedBy)
VALUES (@CustomerUserId, core.fn_FormatBusinessNumber('CUS', NEXT VALUE FOR core.seq_CustomerNumber, @Now), 2, N'Chetan Kumar', N'Chetan Traders', N'customer@procargo.test', '+919900000010', @CustomerUserId);
SET @CustomerId = SCOPE_IDENTITY();

DECLARE @Blr INT = (SELECT CityId FROM mst.City WHERE Name = N'Bengaluru');
DECLARE @Mys INT = (SELECT CityId FROM mst.City WHERE Name = N'Mysuru');
DECLARE @Hbl INT = (SELECT CityId FROM mst.City WHERE Name = N'Hubballi');

INSERT INTO core.CustomerAddress (CustomerId, Label, AddressLine1, Landmark, CityId, Pincode, Latitude, Longitude, IsDefault, CreatedBy)
VALUES (@CustomerId, N'Warehouse', N'No. 12, 3rd Cross, Peenya Industrial Area', N'Near Peenya Metro', @Blr, '560058', 13.028500, 77.519700, 1, @CustomerUserId);

-- Vehicle owner (verified) with one verified, available vehicle
DECLARE @OwnerUserId BIGINT, @OwnerId BIGINT;
INSERT INTO sec.[User] (Email, NormalizedEmail, PhoneNumber, FullName, PasswordHash, IsInternal, EmailConfirmed)
VALUES (N'owner@procargo.test', N'OWNER@PROCARGO.TEST', '+919900000020', N'Olekar Transport', N'AQAAAAIAAYagAAAAEO2Z7ZAOZrG4JlJtlo/bpKnRhvo170HHNQ8PaUZNRCzqpcrGcEx27C331rM2iS5bZw==', 0, 1);
SET @OwnerUserId = SCOPE_IDENTITY();
INSERT INTO sec.UserRole (UserId, RoleId) SELECT @OwnerUserId, RoleId FROM sec.Role WHERE Name = 'VehicleOwner';
INSERT INTO core.VehicleOwner (UserId, OwnerNumber, OwnerTypeId, FullName, Email, PhoneNumber, VerificationStatusId, VerifiedDateUtc, CreatedBy)
VALUES (@OwnerUserId, core.fn_FormatBusinessNumber('OWN', NEXT VALUE FOR core.seq_OwnerNumber, @Now), 2, N'Ramesh Patil', N'owner@procargo.test', '+919900000020', 3, @Now, @OwnerUserId);
SET @OwnerId = SCOPE_IDENTITY();
INSERT INTO core.OwnerBusiness (OwnerId, BusinessName, FleetSize, CreatedBy) VALUES (@OwnerId, N'Olekar Transport', 4, @OwnerUserId);
INSERT INTO core.OwnerAddress (OwnerId, AddressLine1, CityId, Pincode, IsPrimary, CreatedBy)
VALUES (@OwnerId, N'Plot 7, APMC Yard Road', @Hbl, '580025', 1, @OwnerUserId);

INSERT INTO core.Vehicle (OwnerId, VehicleNumber, VehicleTypeId, Manufacturer, Model, ManufactureYear, CapacityKg, PermitNumber, PermitExpiryDate,
                          InsuranceNumber, InsuranceExpiryDate, FitnessExpiryDate, PucExpiryDate, VerificationStatusId, VerifiedDateUtc, IsAvailable, CreatedBy)
SELECT @OwnerId, 'KA25AB1234', VehicleTypeId, N'Tata Motors', N'LPT 1109', 2022, 7000, 'KA/NP/2024/0098', DATEADD(YEAR, 2, @Now),
       'POL-77812-2026', DATEADD(MONTH, 10, @Now), DATEADD(YEAR, 1, @Now), DATEADD(MONTH, 5, @Now), 3, @Now, 1, @OwnerUserId
FROM mst.VehicleType WHERE Code = 'TRUCK_19FT';

-- Driver employed by the owner (verified, available)
DECLARE @DriverUserId BIGINT, @DriverId BIGINT;
INSERT INTO sec.[User] (Email, NormalizedEmail, PhoneNumber, FullName, PasswordHash, IsInternal, EmailConfirmed)
VALUES (N'driver@procargo.test', N'DRIVER@PROCARGO.TEST', '+919900000030', N'Devaraj Naik', N'AQAAAAIAAYagAAAAEIQzrkA5CwmYw/HnCedzWShhWAIfzUabGbo3b5QGLL6VPFTdEG/uwpq1+dQLt3cXKg==', 0, 1);
SET @DriverUserId = SCOPE_IDENTITY();
INSERT INTO sec.UserRole (UserId, RoleId) SELECT @DriverUserId, RoleId FROM sec.Role WHERE Name = 'Driver';
INSERT INTO core.Driver (UserId, DriverNumber, OwnerId, FullName, PhoneNumber, VerificationStatusId, VerifiedDateUtc, AvailabilityStatusId, CreatedBy)
VALUES (@DriverUserId, core.fn_FormatBusinessNumber('DRV', NEXT VALUE FOR core.seq_DriverNumber, @Now), @OwnerId, N'Devaraj Naik', '+919900000030', 3, @Now, 1, @OwnerUserId);
SET @DriverId = SCOPE_IDENTITY();
INSERT INTO core.DriverLicense (DriverId, LicenseNumber, LicenseClass, IssueDate, ExpiryDate, IssuingAuthority, IsCurrent, CreatedBy)
VALUES (@DriverId, 'KA2520190012345', 'HGMV / HTV', '2019-03-01', DATEADD(YEAR, 3, @Now), N'RTO Hubballi', 1, @OwnerUserId);

-- A submitted booking Bengaluru -> Mysuru waiting for a quotation
DECLARE @BookingId BIGINT;
INSERT INTO core.Booking (BookingNumber, CustomerId, VehicleTypeId, GoodsTypeId, GoodsDescription, TotalWeightKg, TotalQuantity,
                          RequestedPickupDateUtc, SpecialInstructions, EstimatedDistanceKm, BookingStatusId, CreatedBy)
SELECT core.fn_FormatBusinessNumber('BKG', NEXT VALUE FOR core.seq_BookingNumber, @Now), @CustomerId, vt.VehicleTypeId, gt.GoodsTypeId,
       N'Packaged FMCG cartons', 4200, 280, DATEADD(DAY, 2, @Now), N'Handle with care, no stacking above 6 cartons', 145, 2, @CustomerUserId
FROM mst.VehicleType AS vt CROSS JOIN mst.GoodsType AS gt
WHERE vt.Code = 'TRUCK_19FT' AND gt.Code = 'FMCG';
SET @BookingId = SCOPE_IDENTITY();

INSERT INTO core.BookingAddress (BookingId, AddressTypeId, AddressLine1, Landmark, CityId, Pincode, Latitude, Longitude)
VALUES (@BookingId, 1, N'No. 12, 3rd Cross, Peenya Industrial Area', N'Near Peenya Metro', @Blr, '560058', 13.028500, 77.519700),
       (@BookingId, 2, N'KIADB Industrial Area, Hebbal', N'Opp. Infosys gate 2', @Mys, '570016', 12.352800, 76.611500);
INSERT INTO core.BookingContact (BookingId, ContactTypeId, ContactName, PhoneNumber)
VALUES (@BookingId, 1, N'Chetan Kumar', '+919900000010'), (@BookingId, 2, N'Manjunath R', '+919900000011');
INSERT INTO core.BookingItem (BookingId, Description, Quantity, WeightKg, IsFragile)
VALUES (@BookingId, N'Biscuit cartons', 200, 3000, 0), (@BookingId, N'Beverage crates', 80, 1200, 1);
INSERT INTO core.BookingStatusHistory (BookingId, FromStatusId, ToStatusId, Remarks, ChangedBy)
VALUES (@BookingId, NULL, 2, N'Booking created', @CustomerUserId);

COMMIT TRANSACTION;
PRINT 'Test data created.';
GO
