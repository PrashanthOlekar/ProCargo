/*
    Sequences used to generate human-readable business numbers (e.g. PC-BKG-2026-000125).
    NEXT VALUE FOR is atomic and concurrency-safe, unlike SELECT MAX(Id) + 1.
    Gaps are expected (rolled back transactions consume values) and are acceptable for business numbers.
*/
IF OBJECT_ID(N'core.seq_CustomerNumber',  N'SO') IS NULL CREATE SEQUENCE core.seq_CustomerNumber  AS BIGINT START WITH 1 INCREMENT BY 1 CACHE 20;
IF OBJECT_ID(N'core.seq_OwnerNumber',     N'SO') IS NULL CREATE SEQUENCE core.seq_OwnerNumber     AS BIGINT START WITH 1 INCREMENT BY 1 CACHE 20;
IF OBJECT_ID(N'core.seq_DriverNumber',    N'SO') IS NULL CREATE SEQUENCE core.seq_DriverNumber    AS BIGINT START WITH 1 INCREMENT BY 1 CACHE 20;
IF OBJECT_ID(N'core.seq_BookingNumber',   N'SO') IS NULL CREATE SEQUENCE core.seq_BookingNumber   AS BIGINT START WITH 1 INCREMENT BY 1 CACHE 50;
IF OBJECT_ID(N'core.seq_QuotationNumber', N'SO') IS NULL CREATE SEQUENCE core.seq_QuotationNumber AS BIGINT START WITH 1 INCREMENT BY 1 CACHE 50;
IF OBJECT_ID(N'core.seq_TripNumber',      N'SO') IS NULL CREATE SEQUENCE core.seq_TripNumber      AS BIGINT START WITH 1 INCREMENT BY 1 CACHE 50;
IF OBJECT_ID(N'fin.seq_InvoiceNumber',    N'SO') IS NULL CREATE SEQUENCE fin.seq_InvoiceNumber    AS BIGINT START WITH 1 INCREMENT BY 1 NO CACHE;
IF OBJECT_ID(N'fin.seq_PaymentNumber',    N'SO') IS NULL CREATE SEQUENCE fin.seq_PaymentNumber    AS BIGINT START WITH 1 INCREMENT BY 1 CACHE 50;
IF OBJECT_ID(N'fin.seq_RefundNumber',     N'SO') IS NULL CREATE SEQUENCE fin.seq_RefundNumber     AS BIGINT START WITH 1 INCREMENT BY 1 CACHE 20;
IF OBJECT_ID(N'fin.seq_SettlementNumber', N'SO') IS NULL CREATE SEQUENCE fin.seq_SettlementNumber AS BIGINT START WITH 1 INCREMENT BY 1 CACHE 20;
IF OBJECT_ID(N'sup.seq_ComplaintNumber',  N'SO') IS NULL CREATE SEQUENCE sup.seq_ComplaintNumber  AS BIGINT START WITH 1 INCREMENT BY 1 CACHE 20;
IF OBJECT_ID(N'sup.seq_TicketNumber',     N'SO') IS NULL CREATE SEQUENCE sup.seq_TicketNumber     AS BIGINT START WITH 1 INCREMENT BY 1 CACHE 20;
GO
