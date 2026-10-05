/*
    Reporting views. They flatten the normalised model for report and dashboard procedures.
    The application never queries views directly - only through rpt.usp_* procedures.
*/

CREATE OR ALTER VIEW rpt.vw_BookingOverview
AS
SELECT
    b.BookingId,
    b.BookingNumber,
    b.CustomerId,
    c.FullName              AS CustomerName,
    c.CompanyName,
    b.BookingStatusId,
    bs.Name                 AS BookingStatusName,
    b.VehicleTypeId,
    vt.Name                 AS VehicleTypeName,
    pa.CityId               AS PickupCityId,
    pc.Name                 AS PickupCityName,
    da.CityId               AS DeliveryCityId,
    dc.Name                 AS DeliveryCityName,
    b.TotalWeightKg,
    b.RequestedPickupDateUtc,
    b.CreatedDateUtc
FROM core.Booking AS b
INNER JOIN core.Customer       AS c  ON c.CustomerId = b.CustomerId
INNER JOIN mst.BookingStatus   AS bs ON bs.BookingStatusId = b.BookingStatusId
INNER JOIN mst.VehicleType     AS vt ON vt.VehicleTypeId = b.VehicleTypeId
INNER JOIN core.BookingAddress AS pa ON pa.BookingId = b.BookingId AND pa.AddressTypeId = 1
INNER JOIN mst.City            AS pc ON pc.CityId = pa.CityId
INNER JOIN core.BookingAddress AS da ON da.BookingId = b.BookingId AND da.AddressTypeId = 2
INNER JOIN mst.City            AS dc ON dc.CityId = da.CityId;
GO

CREATE OR ALTER VIEW rpt.vw_TripOverview
AS
SELECT
    t.TripId,
    t.TripNumber,
    t.BookingId,
    b.BookingNumber,
    b.CustomerId,
    t.OwnerId,
    o.FullName              AS OwnerName,
    t.VehicleId,
    v.VehicleNumber,
    t.DriverId,
    d.FullName              AS DriverName,
    t.TripStatusId,
    ts.Name                 AS TripStatusName,
    t.PlannedPickupDateUtc,
    t.ActualPickupDateUtc,
    t.PlannedDeliveryDateUtc,
    t.ActualDeliveryDateUtc,
    CASE WHEN t.ActualDeliveryDateUtc IS NOT NULL AND t.ActualDeliveryDateUtc <= t.PlannedDeliveryDateUtc THEN 1 ELSE 0 END AS IsOnTime,
    t.CreatedDateUtc
FROM core.Trip AS t
INNER JOIN core.Booking      AS b  ON b.BookingId = t.BookingId
INNER JOIN core.VehicleOwner AS o  ON o.OwnerId = t.OwnerId
INNER JOIN core.Vehicle      AS v  ON v.VehicleId = t.VehicleId
INNER JOIN core.Driver       AS d  ON d.DriverId = t.DriverId
INNER JOIN mst.TripStatus    AS ts ON ts.TripStatusId = t.TripStatusId;
GO

CREATE OR ALTER VIEW rpt.vw_VehicleCompliance
AS
SELECT
    v.VehicleId,
    v.VehicleNumber,
    v.OwnerId,
    o.FullName AS OwnerName,
    v.VehicleTypeId,
    v.InsuranceExpiryDate,
    v.PermitExpiryDate,
    v.FitnessExpiryDate,
    v.PucExpiryDate,
    x.NextExpiryDate,
    DATEDIFF(DAY, CAST(SYSUTCDATETIME() AS DATE), x.NextExpiryDate) AS DaysToNextExpiry
FROM core.Vehicle AS v
INNER JOIN core.VehicleOwner AS o ON o.OwnerId = v.OwnerId
CROSS APPLY
(
    SELECT MIN(d.ExpiryDate) AS NextExpiryDate
    FROM (VALUES (v.InsuranceExpiryDate), (v.PermitExpiryDate), (v.FitnessExpiryDate), (v.PucExpiryDate)) AS d(ExpiryDate)
) AS x
WHERE v.IsDeleted = 0;
GO

CREATE OR ALTER VIEW rpt.vw_DailyRevenue
AS
SELECT
    CAST(i.InvoiceDateUtc AS DATE) AS RevenueDate,
    COUNT_BIG(*)                   AS InvoiceCount,
    SUM(i.SubTotal)                AS SubTotal,
    SUM(i.TaxAmount)               AS TaxAmount,
    SUM(i.TotalAmount)             AS TotalAmount,
    SUM(i.PaidAmount)              AS PaidAmount
FROM fin.Invoice AS i
WHERE i.InvoiceStatusId <> 5
GROUP BY CAST(i.InvoiceDateUtc AS DATE);
GO
