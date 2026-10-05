/*
    Module: Dashboards and reports (schema rpt)
    All date parameters are UTC; the API converts the user's local date range (IST) to UTC before calling.
*/

CREATE OR ALTER PROCEDURE rpt.usp_Dashboard_GetOperationsKpis
    @TodayStartUtc  DATETIME2(3),
    @MonthStartUtc  DATETIME2(3)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        (SELECT COUNT(*) FROM core.Booking)                                                         AS TotalBookings,
        (SELECT COUNT(*) FROM core.Booking WHERE CreatedDateUtc >= @TodayStartUtc)                  AS TodaysBookings,
        (SELECT COUNT(*) FROM core.Booking WHERE BookingStatusId IN (2, 3))                         AS PendingBookings,
        (SELECT COUNT(*) FROM core.Quotation WHERE QuotationStatusId = 2)                           AS PendingQuotations,
        (SELECT COUNT(*) FROM core.Booking WHERE BookingStatusId = 5)                               AS AwaitingAssignment,
        (SELECT COUNT(*) FROM core.Trip WHERE TripStatusId IN (1, 2, 3, 9, 10))                     AS ActiveTrips,
        (SELECT COUNT(*) FROM core.Trip WHERE TripStatusId = 10)                                    AS TripExceptions,
        (SELECT COUNT(*) FROM core.Trip WHERE TripStatusId IN (6, 7))                               AS CompletedTrips,
        (SELECT COUNT(*) FROM core.Trip WHERE TripStatusId = 8)                                     AS CancelledTrips,
        (SELECT COUNT(*) FROM core.VehicleOwner WHERE VerificationStatusId IN (1, 2) AND IsDeleted = 0) AS PendingOwnerApprovals,
        (SELECT COUNT(*) FROM core.Driver WHERE VerificationStatusId IN (1, 2) AND IsDeleted = 0)   AS PendingDriverApprovals,
        (SELECT COUNT(*) FROM core.Vehicle WHERE VerificationStatusId IN (1, 2) AND IsDeleted = 0)  AS PendingVehicleApprovals,
        (SELECT COUNT(*) FROM fin.Invoice WHERE InvoiceStatusId IN (2, 3))                          AS UnpaidInvoices,
        CAST((SELECT ISNULL(SUM(TotalAmount - PaidAmount), 0) FROM fin.Invoice WHERE InvoiceStatusId IN (2, 3)) AS DECIMAL(14,2)) AS OutstandingAmount,
        (SELECT COUNT(*) FROM fin.Settlement WHERE SettlementStatusId IN (1, 2, 3))                 AS PendingSettlements,
        CAST((SELECT ISNULL(SUM(NetAmount), 0) FROM fin.Settlement WHERE SettlementStatusId IN (1, 2, 3)) AS DECIMAL(14,2)) AS PendingSettlementAmount,
        CAST((SELECT ISNULL(SUM(TotalAmount), 0) FROM fin.Invoice WHERE InvoiceStatusId <> 5 AND InvoiceDateUtc >= @MonthStartUtc) AS DECIMAL(14,2)) AS RevenueThisMonth,
        CAST((SELECT ISNULL(SUM(Amount), 0) FROM fin.Payment WHERE PaymentStatusId IN (4, 6, 7) AND PaymentDateUtc >= @MonthStartUtc) AS DECIMAL(14,2)) AS CollectionsThisMonth,
        (SELECT COUNT(*) FROM sup.SupportTicket WHERE TicketStatusId IN (1, 2, 3))                  AS OpenTickets,
        (SELECT COUNT(*) FROM sup.Complaint WHERE ComplaintStatusId IN (1, 2, 3))                   AS OpenComplaints,
        (SELECT COUNT(*) FROM rpt.vw_VehicleCompliance WHERE DaysToNextExpiry <= 30)                AS VehiclesWithExpiringDocuments;
END
GO

CREATE OR ALTER PROCEDURE rpt.usp_Dashboard_GetCustomerSummary
    @CustomerId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        (SELECT COUNT(*) FROM core.Booking WHERE CustomerId = @CustomerId AND BookingStatusId IN (1, 2, 3, 4, 5, 6, 7, 13)) AS ActiveBookings,
        (SELECT COUNT(*) FROM core.Booking WHERE CustomerId = @CustomerId AND BookingStatusId = 4)  AS AwaitingQuotationResponse,
        (SELECT COUNT(*) FROM core.Booking WHERE CustomerId = @CustomerId AND BookingStatusId = 7)  AS InTransit,
        (SELECT COUNT(*) FROM core.Booking WHERE CustomerId = @CustomerId AND BookingStatusId IN (8, 9, 10, 11)) AS CompletedBookings,
        CAST((SELECT ISNULL(SUM(TotalAmount - PaidAmount), 0) FROM fin.Invoice WHERE CustomerId = @CustomerId AND InvoiceStatusId IN (2, 3)) AS DECIMAL(14,2)) AS OutstandingAmount,
        CAST((SELECT ISNULL(SUM(PaidAmount), 0) FROM fin.Invoice WHERE CustomerId = @CustomerId AND InvoiceStatusId <> 5) AS DECIMAL(14,2)) AS TotalPaid;
END
GO

CREATE OR ALTER PROCEDURE rpt.usp_Dashboard_GetOwnerSummary
    @OwnerId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        (SELECT COUNT(*) FROM core.Vehicle WHERE OwnerId = @OwnerId AND IsDeleted = 0)                              AS TotalVehicles,
        (SELECT COUNT(*) FROM core.Vehicle WHERE OwnerId = @OwnerId AND IsDeleted = 0 AND IsAvailable = 1)          AS AvailableVehicles,
        (SELECT COUNT(*) FROM core.Vehicle WHERE OwnerId = @OwnerId AND IsDeleted = 0 AND VerificationStatusId IN (1, 2)) AS VehiclesPendingVerification,
        (SELECT COUNT(*) FROM core.Driver WHERE OwnerId = @OwnerId AND IsDeleted = 0)                               AS TotalDrivers,
        (SELECT COUNT(*) FROM core.Trip WHERE OwnerId = @OwnerId AND TripStatusId IN (1, 2, 3, 9, 10))              AS ActiveTrips,
        (SELECT COUNT(*) FROM core.Trip WHERE OwnerId = @OwnerId AND TripStatusId IN (4, 5, 6, 7))                  AS CompletedTrips,
        CAST((SELECT ISNULL(SUM(NetAmount), 0) FROM fin.Settlement WHERE OwnerId = @OwnerId AND SettlementStatusId IN (1, 2, 3)) AS DECIMAL(14,2)) AS PendingSettlementAmount,
        CAST((SELECT ISNULL(SUM(NetAmount), 0) FROM fin.Settlement WHERE OwnerId = @OwnerId AND SettlementStatusId = 4) AS DECIMAL(14,2)) AS TotalEarned;
END
GO

CREATE OR ALTER PROCEDURE rpt.usp_Dashboard_GetDriverSummary
    @DriverId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        (SELECT COUNT(*) FROM core.Trip WHERE DriverId = @DriverId AND TripStatusId IN (2, 3, 9, 10)) AS ActiveTrips,
        (SELECT COUNT(*) FROM core.Trip WHERE DriverId = @DriverId AND TripStatusId = 1)               AS UpcomingTrips,
        (SELECT COUNT(*) FROM core.Trip WHERE DriverId = @DriverId AND TripStatusId IN (4, 5, 6, 7))   AS CompletedTrips,
        d.AvailabilityStatusId
    FROM core.Driver AS d
    WHERE d.DriverId = @DriverId;
END
GO

CREATE OR ALTER PROCEDURE rpt.usp_Report_BookingsByDay
    @FromUtc DATETIME2(3),
    @ToUtc   DATETIME2(3)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        CAST(b.CreatedDateUtc AS DATE) AS ReportDate,
        COUNT(*) AS TotalBookings,
        SUM(CASE WHEN b.BookingStatusId IN (5, 6, 7, 8, 9, 10, 11) THEN 1 ELSE 0 END) AS ConfirmedBookings,
        SUM(CASE WHEN b.BookingStatusId IN (12, 14) THEN 1 ELSE 0 END) AS CancelledOrRejected
    FROM core.Booking AS b
    WHERE b.CreatedDateUtc >= @FromUtc AND b.CreatedDateUtc < @ToUtc
    GROUP BY CAST(b.CreatedDateUtc AS DATE)
    ORDER BY ReportDate;
END
GO

CREATE OR ALTER PROCEDURE rpt.usp_Report_BookingsByStatus
    @FromUtc DATETIME2(3),
    @ToUtc   DATETIME2(3)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT bs.BookingStatusId AS StatusId, bs.Name AS StatusName, COUNT(b.BookingId) AS ItemCount
    FROM mst.BookingStatus AS bs
    LEFT JOIN core.Booking AS b ON b.BookingStatusId = bs.BookingStatusId AND b.CreatedDateUtc >= @FromUtc AND b.CreatedDateUtc < @ToUtc
    GROUP BY bs.BookingStatusId, bs.Name, bs.SortOrder
    ORDER BY bs.SortOrder;
END
GO

CREATE OR ALTER PROCEDURE rpt.usp_Report_Revenue
    @FromUtc  DATETIME2(3),
    @ToUtc    DATETIME2(3),
    @GroupBy  VARCHAR(10)      -- Day | Month
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        CASE WHEN @GroupBy = 'Month' THEN DATEFROMPARTS(YEAR(dr.RevenueDate), MONTH(dr.RevenueDate), 1) ELSE dr.RevenueDate END AS Period,
        CAST(SUM(dr.InvoiceCount) AS INT)    AS InvoiceCount,
        CAST(SUM(dr.SubTotal) AS DECIMAL(14,2))    AS SubTotal,
        CAST(SUM(dr.TaxAmount) AS DECIMAL(14,2))   AS TaxAmount,
        CAST(SUM(dr.TotalAmount) AS DECIMAL(14,2)) AS TotalAmount,
        CAST(SUM(dr.PaidAmount) AS DECIMAL(14,2))  AS PaidAmount
    FROM rpt.vw_DailyRevenue AS dr
    WHERE dr.RevenueDate >= CAST(@FromUtc AS DATE) AND dr.RevenueDate < CAST(@ToUtc AS DATE)
    GROUP BY CASE WHEN @GroupBy = 'Month' THEN DATEFROMPARTS(YEAR(dr.RevenueDate), MONTH(dr.RevenueDate), 1) ELSE dr.RevenueDate END
    ORDER BY Period;
END
GO

CREATE OR ALTER PROCEDURE rpt.usp_Report_TripPerformance
    @FromUtc DATETIME2(3),
    @ToUtc   DATETIME2(3)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        vt.VehicleTypeId,
        vt.Name AS VehicleTypeName,
        COUNT(*) AS TripCount,
        SUM(CASE WHEN t.ActualDeliveryDateUtc IS NOT NULL THEN 1 ELSE 0 END) AS DeliveredCount,
        SUM(CASE WHEN t.ActualDeliveryDateUtc IS NOT NULL AND t.ActualDeliveryDateUtc <= t.PlannedDeliveryDateUtc THEN 1 ELSE 0 END) AS OnTimeCount,
        SUM(CASE WHEN t.TripStatusId = 8 THEN 1 ELSE 0 END) AS CancelledCount,
        CAST(AVG(CASE WHEN t.ActualDeliveryDateUtc IS NOT NULL AND t.ActualPickupDateUtc IS NOT NULL
                      THEN DATEDIFF(MINUTE, t.ActualPickupDateUtc, t.ActualDeliveryDateUtc) / 60.0 END) AS DECIMAL(10,2)) AS AverageTransitHours
    FROM core.Trip AS t
    INNER JOIN core.Vehicle    AS v  ON v.VehicleId = t.VehicleId
    INNER JOIN mst.VehicleType AS vt ON vt.VehicleTypeId = v.VehicleTypeId
    WHERE t.PlannedPickupDateUtc >= @FromUtc AND t.PlannedPickupDateUtc < @ToUtc
    GROUP BY vt.VehicleTypeId, vt.Name, vt.SortOrder
    ORDER BY vt.SortOrder;
END
GO

CREATE OR ALTER PROCEDURE rpt.usp_Report_Settlements
    @FromUtc DATETIME2(3),
    @ToUtc   DATETIME2(3)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ss.SettlementStatusId AS StatusId, ss.Name AS StatusName, COUNT(s.SettlementId) AS SettlementCount,
           CAST(ISNULL(SUM(s.GrossAmount), 0) AS DECIMAL(14,2)) AS GrossAmount,
           CAST(ISNULL(SUM(s.CommissionAmount), 0) AS DECIMAL(14,2)) AS CommissionAmount,
           CAST(ISNULL(SUM(s.NetAmount), 0) AS DECIMAL(14,2)) AS NetAmount
    FROM mst.SettlementStatus AS ss
    LEFT JOIN fin.Settlement AS s ON s.SettlementStatusId = ss.SettlementStatusId AND s.CreatedDateUtc >= @FromUtc AND s.CreatedDateUtc < @ToUtc
    GROUP BY ss.SettlementStatusId, ss.Name, ss.SortOrder
    ORDER BY ss.SortOrder;
END
GO

CREATE OR ALTER PROCEDURE rpt.usp_Report_TopCustomers
    @FromUtc DATETIME2(3),
    @ToUtc   DATETIME2(3),
    @Top     INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@Top)
        c.CustomerId,
        c.CustomerNumber,
        c.FullName AS CustomerName,
        COUNT(DISTINCT b.BookingId) AS BookingCount,
        CAST(ISNULL(SUM(i.TotalAmount), 0) AS DECIMAL(14,2)) AS InvoicedAmount
    FROM core.Customer AS c
    INNER JOIN core.Booking AS b ON b.CustomerId = c.CustomerId AND b.CreatedDateUtc >= @FromUtc AND b.CreatedDateUtc < @ToUtc
    LEFT  JOIN fin.Invoice  AS i ON i.BookingId = b.BookingId AND i.InvoiceStatusId <> 5
    GROUP BY c.CustomerId, c.CustomerNumber, c.FullName
    ORDER BY InvoicedAmount DESC, BookingCount DESC;
END
GO

-- Partner population by verification/activity status, for the "customers / owners / drivers / vehicles" reports.
CREATE OR ALTER PROCEDURE rpt.usp_Report_PartnerSummary
AS
BEGIN
    SET NOCOUNT ON;
    SELECT 'Customers' AS EntityType, CASE WHEN c.IsActive = 1 THEN N'Active' ELSE N'Inactive' END AS StatusName, COUNT(*) AS ItemCount
    FROM core.Customer AS c WHERE c.IsDeleted = 0 GROUP BY c.IsActive
    UNION ALL
    SELECT 'Owners', vs.Name, COUNT(*) FROM core.VehicleOwner AS o
    INNER JOIN mst.VerificationStatus AS vs ON vs.VerificationStatusId = o.VerificationStatusId
    WHERE o.IsDeleted = 0 GROUP BY vs.Name
    UNION ALL
    SELECT 'Drivers', vs.Name, COUNT(*) FROM core.Driver AS d
    INNER JOIN mst.VerificationStatus AS vs ON vs.VerificationStatusId = d.VerificationStatusId
    WHERE d.IsDeleted = 0 GROUP BY vs.Name
    UNION ALL
    SELECT 'Vehicles', vs.Name, COUNT(*) FROM core.Vehicle AS v
    INNER JOIN mst.VerificationStatus AS vs ON vs.VerificationStatusId = v.VerificationStatusId
    WHERE v.IsDeleted = 0 GROUP BY vs.Name;
END
GO
