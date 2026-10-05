/*
    System lookup values. Ids MUST match the enums in ProCargo.Domain/Enums.
    MERGE keeps the script re-runnable: names/sort order are refreshed, ids never change.
*/

MERGE mst.BookingStatus AS target
USING (VALUES
        (1, 'Draft', N'Draft', 1, 0),
        (2, 'Submitted', N'Submitted', 2, 0),
        (3, 'UnderReview', N'Under Review', 3, 0),
        (4, 'Quoted', N'Quoted', 4, 0),
        (5, 'Confirmed', N'Confirmed', 5, 0),
        (6, 'Assigned', N'Assigned', 6, 0),
        (7, 'InTransit', N'In Transit', 7, 0),
        (8, 'Delivered', N'Delivered', 8, 0),
        (9, 'Invoiced', N'Invoiced', 9, 0),
        (10, 'Paid', N'Paid', 10, 0),
        (11, 'Closed', N'Closed', 11, 1),
        (12, 'Cancelled', N'Cancelled', 12, 1),
        (13, 'OnHold', N'On Hold', 13, 0),
        (14, 'Rejected', N'Rejected', 14, 1)
) AS source (BookingStatusId, Code, Name, SortOrder, IsTerminal)
ON target.BookingStatusId = source.BookingStatusId
WHEN MATCHED THEN UPDATE SET Code = source.Code, Name = source.Name, SortOrder = source.SortOrder, IsTerminal = source.IsTerminal
WHEN NOT MATCHED BY TARGET THEN INSERT (BookingStatusId, Code, Name, SortOrder, IsTerminal) VALUES (source.BookingStatusId, source.Code, source.Name, source.SortOrder, source.IsTerminal);
GO

MERGE mst.TripStatus AS target
USING (VALUES
        (1, 'Scheduled', N'Scheduled', 1, 0),
        (2, 'PickupVerified', N'Pickup Verified', 2, 0),
        (3, 'InTransit', N'In Transit', 3, 0),
        (4, 'Delivered', N'Delivered', 4, 0),
        (5, 'PodUploaded', N'POD Uploaded', 5, 0),
        (6, 'Completed', N'Completed', 6, 0),
        (7, 'Closed', N'Closed', 7, 1),
        (8, 'Cancelled', N'Cancelled', 8, 1),
        (9, 'OnHold', N'On Hold', 9, 0),
        (10, 'Exception', N'Exception', 10, 0)
) AS source (TripStatusId, Code, Name, SortOrder, IsTerminal)
ON target.TripStatusId = source.TripStatusId
WHEN MATCHED THEN UPDATE SET Code = source.Code, Name = source.Name, SortOrder = source.SortOrder, IsTerminal = source.IsTerminal
WHEN NOT MATCHED BY TARGET THEN INSERT (TripStatusId, Code, Name, SortOrder, IsTerminal) VALUES (source.TripStatusId, source.Code, source.Name, source.SortOrder, source.IsTerminal);
GO

MERGE mst.QuotationStatus AS target
USING (VALUES
        (1, 'Draft', N'Draft', 1, 0),
        (2, 'Sent', N'Sent', 2, 0),
        (3, 'Accepted', N'Accepted', 3, 1),
        (4, 'Rejected', N'Rejected', 4, 1),
        (5, 'Expired', N'Expired', 5, 1),
        (6, 'Withdrawn', N'Withdrawn', 6, 1)
) AS source (QuotationStatusId, Code, Name, SortOrder, IsTerminal)
ON target.QuotationStatusId = source.QuotationStatusId
WHEN MATCHED THEN UPDATE SET Code = source.Code, Name = source.Name, SortOrder = source.SortOrder, IsTerminal = source.IsTerminal
WHEN NOT MATCHED BY TARGET THEN INSERT (QuotationStatusId, Code, Name, SortOrder, IsTerminal) VALUES (source.QuotationStatusId, source.Code, source.Name, source.SortOrder, source.IsTerminal);
GO

MERGE mst.PaymentStatus AS target
USING (VALUES
        (1, 'Pending', N'Pending', 1, 0),
        (2, 'Initiated', N'Initiated', 2, 0),
        (3, 'Authorized', N'Authorized', 3, 0),
        (4, 'Paid', N'Paid', 4, 0),
        (5, 'Failed', N'Failed', 5, 1),
        (6, 'Refunded', N'Refunded', 6, 1),
        (7, 'PartiallyRefunded', N'Partially Refunded', 7, 0),
        (8, 'PartiallyPaid', N'Partially Paid', 8, 0),
        (9, 'Cancelled', N'Cancelled', 9, 1)
) AS source (PaymentStatusId, Code, Name, SortOrder, IsTerminal)
ON target.PaymentStatusId = source.PaymentStatusId
WHEN MATCHED THEN UPDATE SET Code = source.Code, Name = source.Name, SortOrder = source.SortOrder, IsTerminal = source.IsTerminal
WHEN NOT MATCHED BY TARGET THEN INSERT (PaymentStatusId, Code, Name, SortOrder, IsTerminal) VALUES (source.PaymentStatusId, source.Code, source.Name, source.SortOrder, source.IsTerminal);
GO

MERGE mst.InvoiceStatus AS target
USING (VALUES
        (1, 'Draft', N'Draft', 1, 0),
        (2, 'Issued', N'Issued', 2, 0),
        (3, 'PartiallyPaid', N'Partially Paid', 3, 0),
        (4, 'Paid', N'Paid', 4, 1),
        (5, 'Cancelled', N'Cancelled', 5, 1)
) AS source (InvoiceStatusId, Code, Name, SortOrder, IsTerminal)
ON target.InvoiceStatusId = source.InvoiceStatusId
WHEN MATCHED THEN UPDATE SET Code = source.Code, Name = source.Name, SortOrder = source.SortOrder, IsTerminal = source.IsTerminal
WHEN NOT MATCHED BY TARGET THEN INSERT (InvoiceStatusId, Code, Name, SortOrder, IsTerminal) VALUES (source.InvoiceStatusId, source.Code, source.Name, source.SortOrder, source.IsTerminal);
GO

MERGE mst.SettlementStatus AS target
USING (VALUES
        (1, 'Pending', N'Pending', 1, 0),
        (2, 'Approved', N'Approved', 2, 0),
        (3, 'Processing', N'Processing', 3, 0),
        (4, 'Completed', N'Completed', 4, 1),
        (5, 'Failed', N'Failed', 5, 1),
        (6, 'Cancelled', N'Cancelled', 6, 1)
) AS source (SettlementStatusId, Code, Name, SortOrder, IsTerminal)
ON target.SettlementStatusId = source.SettlementStatusId
WHEN MATCHED THEN UPDATE SET Code = source.Code, Name = source.Name, SortOrder = source.SortOrder, IsTerminal = source.IsTerminal
WHEN NOT MATCHED BY TARGET THEN INSERT (SettlementStatusId, Code, Name, SortOrder, IsTerminal) VALUES (source.SettlementStatusId, source.Code, source.Name, source.SortOrder, source.IsTerminal);
GO

MERGE mst.RefundStatus AS target
USING (VALUES
        (1, 'Pending', N'Pending', 1, 0),
        (2, 'Processed', N'Processed', 2, 1),
        (3, 'Failed', N'Failed', 3, 1)
) AS source (RefundStatusId, Code, Name, SortOrder, IsTerminal)
ON target.RefundStatusId = source.RefundStatusId
WHEN MATCHED THEN UPDATE SET Code = source.Code, Name = source.Name, SortOrder = source.SortOrder, IsTerminal = source.IsTerminal
WHEN NOT MATCHED BY TARGET THEN INSERT (RefundStatusId, Code, Name, SortOrder, IsTerminal) VALUES (source.RefundStatusId, source.Code, source.Name, source.SortOrder, source.IsTerminal);
GO

MERGE mst.VerificationStatus AS target
USING (VALUES
        (1, 'Pending', N'Pending', 1, 0),
        (2, 'UnderReview', N'Under Review', 2, 0),
        (3, 'Verified', N'Verified', 3, 0),
        (4, 'Rejected', N'Rejected', 4, 0),
        (5, 'Expired', N'Expired', 5, 0)
) AS source (VerificationStatusId, Code, Name, SortOrder, IsTerminal)
ON target.VerificationStatusId = source.VerificationStatusId
WHEN MATCHED THEN UPDATE SET Code = source.Code, Name = source.Name, SortOrder = source.SortOrder, IsTerminal = source.IsTerminal
WHEN NOT MATCHED BY TARGET THEN INSERT (VerificationStatusId, Code, Name, SortOrder, IsTerminal) VALUES (source.VerificationStatusId, source.Code, source.Name, source.SortOrder, source.IsTerminal);
GO

MERGE mst.DriverAvailabilityStatus AS target
USING (VALUES
        (1, 'Available', N'Available', 1, 0),
        (2, 'OnTrip', N'On Trip', 2, 0),
        (3, 'OffDuty', N'Off Duty', 3, 0),
        (4, 'Unavailable', N'Unavailable', 4, 0)
) AS source (DriverAvailabilityStatusId, Code, Name, SortOrder, IsTerminal)
ON target.DriverAvailabilityStatusId = source.DriverAvailabilityStatusId
WHEN MATCHED THEN UPDATE SET Code = source.Code, Name = source.Name, SortOrder = source.SortOrder, IsTerminal = source.IsTerminal
WHEN NOT MATCHED BY TARGET THEN INSERT (DriverAvailabilityStatusId, Code, Name, SortOrder, IsTerminal) VALUES (source.DriverAvailabilityStatusId, source.Code, source.Name, source.SortOrder, source.IsTerminal);
GO

MERGE mst.TicketStatus AS target
USING (VALUES
        (1, 'Open', N'Open', 1, 0),
        (2, 'InProgress', N'In Progress', 2, 0),
        (3, 'WaitingOnCustomer', N'Waiting on Customer', 3, 0),
        (4, 'Resolved', N'Resolved', 4, 0),
        (5, 'Closed', N'Closed', 5, 1)
) AS source (TicketStatusId, Code, Name, SortOrder, IsTerminal)
ON target.TicketStatusId = source.TicketStatusId
WHEN MATCHED THEN UPDATE SET Code = source.Code, Name = source.Name, SortOrder = source.SortOrder, IsTerminal = source.IsTerminal
WHEN NOT MATCHED BY TARGET THEN INSERT (TicketStatusId, Code, Name, SortOrder, IsTerminal) VALUES (source.TicketStatusId, source.Code, source.Name, source.SortOrder, source.IsTerminal);
GO

MERGE mst.TicketPriority AS target
USING (VALUES
        (1, 'Low', N'Low', 1, 0),
        (2, 'Medium', N'Medium', 2, 0),
        (3, 'High', N'High', 3, 0),
        (4, 'Critical', N'Critical', 4, 0)
) AS source (TicketPriorityId, Code, Name, SortOrder, IsTerminal)
ON target.TicketPriorityId = source.TicketPriorityId
WHEN MATCHED THEN UPDATE SET Code = source.Code, Name = source.Name, SortOrder = source.SortOrder, IsTerminal = source.IsTerminal
WHEN NOT MATCHED BY TARGET THEN INSERT (TicketPriorityId, Code, Name, SortOrder, IsTerminal) VALUES (source.TicketPriorityId, source.Code, source.Name, source.SortOrder, source.IsTerminal);
GO

MERGE mst.ComplaintStatus AS target
USING (VALUES
        (1, 'Open', N'Open', 1, 0),
        (2, 'Assigned', N'Assigned', 2, 0),
        (3, 'Investigating', N'Investigating', 3, 0),
        (4, 'Resolved', N'Resolved', 4, 0),
        (5, 'Rejected', N'Rejected', 5, 1),
        (6, 'Closed', N'Closed', 6, 1)
) AS source (ComplaintStatusId, Code, Name, SortOrder, IsTerminal)
ON target.ComplaintStatusId = source.ComplaintStatusId
WHEN MATCHED THEN UPDATE SET Code = source.Code, Name = source.Name, SortOrder = source.SortOrder, IsTerminal = source.IsTerminal
WHEN NOT MATCHED BY TARGET THEN INSERT (ComplaintStatusId, Code, Name, SortOrder, IsTerminal) VALUES (source.ComplaintStatusId, source.Code, source.Name, source.SortOrder, source.IsTerminal);
GO

MERGE mst.CustomerType AS target
USING (VALUES
        (1, 'Individual', N'Individual', 1, 0),
        (2, 'Business', N'Business', 2, 0)
) AS source (CustomerTypeId, Code, Name, SortOrder, IsTerminal)
ON target.CustomerTypeId = source.CustomerTypeId
WHEN MATCHED THEN UPDATE SET Code = source.Code, Name = source.Name, SortOrder = source.SortOrder, IsTerminal = source.IsTerminal
WHEN NOT MATCHED BY TARGET THEN INSERT (CustomerTypeId, Code, Name, SortOrder, IsTerminal) VALUES (source.CustomerTypeId, source.Code, source.Name, source.SortOrder, source.IsTerminal);
GO

MERGE mst.OwnerType AS target
USING (VALUES
        (1, 'Individual', N'Individual Owner', 1, 0),
        (2, 'FleetBusiness', N'Fleet Business', 2, 0)
) AS source (OwnerTypeId, Code, Name, SortOrder, IsTerminal)
ON target.OwnerTypeId = source.OwnerTypeId
WHEN MATCHED THEN UPDATE SET Code = source.Code, Name = source.Name, SortOrder = source.SortOrder, IsTerminal = source.IsTerminal
WHEN NOT MATCHED BY TARGET THEN INSERT (OwnerTypeId, Code, Name, SortOrder, IsTerminal) VALUES (source.OwnerTypeId, source.Code, source.Name, source.SortOrder, source.IsTerminal);
GO

MERGE mst.PaymentMethod AS target
USING (VALUES
        (1, 'Upi', N'UPI', 1, 0),
        (2, 'Card', N'Card', 2, 0),
        (3, 'NetBanking', N'Net Banking', 3, 0),
        (4, 'Wallet', N'Wallet', 4, 0),
        (5, 'BankTransfer', N'Bank Transfer', 5, 0),
        (6, 'Cash', N'Cash', 6, 0),
        (7, 'Cheque', N'Cheque', 7, 0)
) AS source (PaymentMethodId, Code, Name, SortOrder, IsTerminal)
ON target.PaymentMethodId = source.PaymentMethodId
WHEN MATCHED THEN UPDATE SET Code = source.Code, Name = source.Name, SortOrder = source.SortOrder, IsTerminal = source.IsTerminal
WHEN NOT MATCHED BY TARGET THEN INSERT (PaymentMethodId, Code, Name, SortOrder, IsTerminal) VALUES (source.PaymentMethodId, source.Code, source.Name, source.SortOrder, source.IsTerminal);
GO

MERGE mst.NotificationChannel AS target
USING (VALUES
        (1, 'InApp', N'In-App', 1, 0),
        (2, 'Email', N'Email', 2, 0),
        (3, 'Sms', N'SMS', 3, 0),
        (4, 'Push', N'Push', 4, 0)
) AS source (NotificationChannelId, Code, Name, SortOrder, IsTerminal)
ON target.NotificationChannelId = source.NotificationChannelId
WHEN MATCHED THEN UPDATE SET Code = source.Code, Name = source.Name, SortOrder = source.SortOrder, IsTerminal = source.IsTerminal
WHEN NOT MATCHED BY TARGET THEN INSERT (NotificationChannelId, Code, Name, SortOrder, IsTerminal) VALUES (source.NotificationChannelId, source.Code, source.Name, source.SortOrder, source.IsTerminal);
GO

MERGE mst.NotificationStatus AS target
USING (VALUES
        (1, 'Pending', N'Pending', 1, 0),
        (2, 'Sent', N'Sent', 2, 1),
        (3, 'Failed', N'Failed', 3, 1)
) AS source (NotificationStatusId, Code, Name, SortOrder, IsTerminal)
ON target.NotificationStatusId = source.NotificationStatusId
WHEN MATCHED THEN UPDATE SET Code = source.Code, Name = source.Name, SortOrder = source.SortOrder, IsTerminal = source.IsTerminal
WHEN NOT MATCHED BY TARGET THEN INSERT (NotificationStatusId, Code, Name, SortOrder, IsTerminal) VALUES (source.NotificationStatusId, source.Code, source.Name, source.SortOrder, source.IsTerminal);
GO

MERGE core.TrackingProvider AS target
USING (VALUES (1, 'DriverApp', N'Driver mobile web app'), (2, 'Manual', N'Manual entry by operations'), (3, 'GpsDevice', N'Vehicle GPS device (provider integration)')) AS source (TrackingProviderId, Code, Name)
ON target.TrackingProviderId = source.TrackingProviderId
WHEN MATCHED THEN UPDATE SET Code = source.Code, Name = source.Name
WHEN NOT MATCHED BY TARGET THEN INSERT (TrackingProviderId, Code, Name) VALUES (source.TrackingProviderId, source.Code, source.Name);
GO
