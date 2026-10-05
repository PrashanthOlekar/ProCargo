/*
    Reference data: states, cities, vehicle types, goods types, document types, system settings.
    Re-runnable: rows are inserted only when missing (matched on natural keys).
*/

;WITH src AS (SELECT * FROM (VALUES
    ('AP',N'Andhra Pradesh'),('AR',N'Arunachal Pradesh'),('AS',N'Assam'),('BR',N'Bihar'),('CG',N'Chhattisgarh'),
    ('GA',N'Goa'),('GJ',N'Gujarat'),('HR',N'Haryana'),('HP',N'Himachal Pradesh'),('JH',N'Jharkhand'),
    ('KA',N'Karnataka'),('KL',N'Kerala'),('MP',N'Madhya Pradesh'),('MH',N'Maharashtra'),('MN',N'Manipur'),
    ('ML',N'Meghalaya'),('MZ',N'Mizoram'),('NL',N'Nagaland'),('OD',N'Odisha'),('PB',N'Punjab'),
    ('RJ',N'Rajasthan'),('SK',N'Sikkim'),('TN',N'Tamil Nadu'),('TS',N'Telangana'),('TR',N'Tripura'),
    ('UP',N'Uttar Pradesh'),('UK',N'Uttarakhand'),('WB',N'West Bengal'),
    ('AN',N'Andaman and Nicobar Islands'),('CH',N'Chandigarh'),('DN',N'Dadra and Nagar Haveli and Daman and Diu'),
    ('DL',N'Delhi'),('JK',N'Jammu and Kashmir'),('LA',N'Ladakh'),('LD',N'Lakshadweep'),('PY',N'Puducherry')
) AS v(StateCode, Name))
INSERT INTO mst.State (StateCode, Name)
SELECT s.StateCode, s.Name FROM src AS s
WHERE NOT EXISTS (SELECT 1 FROM mst.State AS x WHERE x.StateCode = s.StateCode);
GO

;WITH src AS (SELECT * FROM (VALUES
    ('KA',N'Bengaluru'),('KA',N'Mysuru'),('KA',N'Hubballi'),('KA',N'Dharwad'),('KA',N'Belagavi'),('KA',N'Mangaluru'),
    ('KA',N'Kalaburagi'),('KA',N'Davanagere'),('KA',N'Ballari'),('KA',N'Vijayapura'),('KA',N'Shivamogga'),('KA',N'Tumakuru'),
    ('KA',N'Udupi'),('KA',N'Hassan'),('KA',N'Raichur'),('KA',N'Bidar'),('KA',N'Chitradurga'),('KA',N'Hosapete'),
    ('MH',N'Mumbai'),('MH',N'Pune'),('MH',N'Nagpur'),('MH',N'Nashik'),
    ('TN',N'Chennai'),('TN',N'Coimbatore'),('TN',N'Madurai'),('TN',N'Hosur'),
    ('TS',N'Hyderabad'),('AP',N'Visakhapatnam'),('AP',N'Vijayawada'),('AP',N'Anantapur'),
    ('KL',N'Kochi'),('KL',N'Thiruvananthapuram'),('KL',N'Kozhikode'),
    ('GA',N'Panaji'),('GA',N'Margao'),('GJ',N'Ahmedabad'),('GJ',N'Surat'),('DL',N'New Delhi'),
    ('WB',N'Kolkata'),('RJ',N'Jaipur'),('UP',N'Lucknow'),('MP',N'Indore')
) AS v(StateCode, Name))
INSERT INTO mst.City (StateId, Name)
SELECT st.StateId, s.Name
FROM src AS s
INNER JOIN mst.State AS st ON st.StateCode = s.StateCode
WHERE NOT EXISTS (SELECT 1 FROM mst.City AS c WHERE c.StateId = st.StateId AND c.Name = s.Name);
GO

;WITH src AS (SELECT * FROM (VALUES
    ('MINI_TRUCK',     N'Mini Truck (Tata Ace)',      N'Small loads within city, up to 750 kg',            750.00,  7.00, 1),
    ('PICKUP_1_5T',    N'Pickup (1.5 Ton)',           N'Pickup truck for city and short intercity',        1500.00, 8.00, 2),
    ('LCV_14FT',       N'14 ft LCV',                  N'Light commercial vehicle, up to 4 tonnes',         4000.00, 14.00, 3),
    ('TRUCK_17FT',     N'17 ft Truck',                N'Medium truck, up to 5 tonnes',                     5000.00, 17.00, 4),
    ('TRUCK_19FT',     N'19 ft Truck',                N'Medium truck, up to 7 tonnes',                     7000.00, 19.00, 5),
    ('CONTAINER_20FT', N'20 ft Container',            N'Closed body container, up to 7 tonnes',            7000.00, 20.00, 6),
    ('TRUCK_22FT',     N'22 ft Truck',                N'Heavy truck, up to 10 tonnes',                     10000.00, 22.00, 7),
    ('CONTAINER_32SXL',N'32 ft Single Axle Container',N'Long container for light, bulky cargo',            9000.00, 32.00, 8),
    ('CONTAINER_32MXL',N'32 ft Multi Axle Container', N'Long container for heavy cargo',                   16000.00, 32.00, 9),
    ('TRAILER_40FT',   N'40 ft Trailer',              N'Flatbed trailer for machinery and steel',          25000.00, 40.00, 10)
) AS v(Code, Name, Description, CapacityKg, LengthFt, SortOrder))
INSERT INTO mst.VehicleType (Code, Name, Description, CapacityKg, LengthFt, SortOrder)
SELECT s.Code, s.Name, s.Description, s.CapacityKg, s.LengthFt, s.SortOrder FROM src AS s
WHERE NOT EXISTS (SELECT 1 FROM mst.VehicleType AS x WHERE x.Code = s.Code);
GO

;WITH src AS (SELECT * FROM (VALUES
    ('GENERAL',      N'General Goods',            0), ('FMCG',        N'FMCG',                     0),
    ('FURNITURE',    N'Furniture & Household',    0), ('ELECTRONICS', N'Electronics',              1),
    ('MACHINERY',    N'Industrial Machinery',     1), ('BUILDING',    N'Building Materials',       0),
    ('AGRI',         N'Agricultural Produce',     0), ('TEXTILES',    N'Textiles & Garments',      0),
    ('CHEMICALS',    N'Chemicals (non-hazardous)',1), ('PERISHABLES', N'Perishables',              1),
    ('AUTO_PARTS',   N'Automobile Parts',         0), ('STEEL',       N'Steel & Metals',           0)
) AS v(Code, Name, RequiresSpecialHandling))
INSERT INTO mst.GoodsType (Code, Name, RequiresSpecialHandling)
SELECT s.Code, s.Name, s.RequiresSpecialHandling FROM src AS s
WHERE NOT EXISTS (SELECT 1 FROM mst.GoodsType AS x WHERE x.Code = s.Code);
GO

;WITH src AS (SELECT * FROM (VALUES
    ('CUST_GST_CERT',       N'GST Registration Certificate', 'Customer', 0, 0),
    ('CUST_PAN',            N'PAN Card',                     'Customer', 0, 0),
    ('OWNER_PAN',           N'PAN Card',                     'Owner',    0, 1),
    ('OWNER_AADHAAR',       N'Aadhaar Card',                 'Owner',    0, 1),
    ('OWNER_GST_CERT',      N'GST Registration Certificate', 'Owner',    0, 0),
    ('OWNER_CANCELLED_CHEQUE', N'Cancelled Cheque',          'Owner',    0, 1),
    ('DRIVER_LICENSE',      N'Driving Licence',              'Driver',   1, 1),
    ('DRIVER_AADHAAR',      N'Aadhaar Card',                 'Driver',   0, 1),
    ('DRIVER_PHOTO',        N'Photograph',                   'Driver',   0, 0),
    ('DRIVER_POLICE_VERIFICATION', N'Police Verification',   'Driver',   1, 0),
    ('VEHICLE_RC',          N'Registration Certificate (RC)','Vehicle',  0, 1),
    ('VEHICLE_INSURANCE',   N'Insurance Policy',             'Vehicle',  1, 1),
    ('VEHICLE_PERMIT',      N'National / State Permit',      'Vehicle',  1, 0),
    ('VEHICLE_FITNESS',     N'Fitness Certificate',          'Vehicle',  1, 0),
    ('VEHICLE_PUC',         N'Pollution Under Control (PUC)','Vehicle',  1, 0),
    ('VEHICLE_PHOTO',       N'Vehicle Photograph',           'Vehicle',  0, 0)
) AS v(Code, Name, AppliesTo, RequiresExpiry, IsMandatory))
INSERT INTO mst.DocumentType (Code, Name, AppliesTo, RequiresExpiry, IsMandatory)
SELECT s.Code, s.Name, s.AppliesTo, s.RequiresExpiry, s.IsMandatory FROM src AS s
WHERE NOT EXISTS (SELECT 1 FROM mst.DocumentType AS x WHERE x.Code = s.Code);
GO

;WITH src AS (SELECT * FROM (VALUES
    ('Booking.MinLeadTimeMinutes',              N'60',    'Int',     N'Minimum minutes between booking creation and requested pickup', 1),
    ('Booking.MaxAdvanceDays',                  N'90',    'Int',     N'How far ahead a pickup can be requested', 1),
    ('Booking.MaxItems',                        N'50',    'Int',     N'Maximum items per booking', 1),
    ('Quotation.DefaultValidityHours',          N'48',    'Int',     N'Default quotation validity', 1),
    ('Quotation.DiscountApprovalThresholdPercent', N'10', 'Decimal', N'Discounts above this % of subtotal need ApproveQuotations', 1),
    ('Invoice.PaymentTermsDays',                N'7',     'Int',     N'Invoice due date = invoice date + N days', 1),
    ('Otp.ValidityMinutes',                     N'10',    'Int',     N'Pickup/delivery OTP validity', 1),
    ('Otp.MaxAttempts',                         N'5',     'Int',     N'Maximum OTP verification attempts', 1),
    ('Settlement.TdsPercent',                   N'1.00',  'Decimal', N'TDS deducted from owner freight (Section 194C) when applicable', 1),
    ('Platform.SupportEmail',                   N'support@procargo.com', 'String', N'Shown to customers', 1),
    ('Platform.SupportPhone',                   N'+918000000000',        'String', N'Shown to customers', 1)
) AS v(SettingKey, SettingValue, DataType, Description, IsEditable))
INSERT INTO mst.SystemSetting (SettingKey, SettingValue, DataType, Description, IsEditable)
SELECT s.SettingKey, s.SettingValue, s.DataType, s.Description, s.IsEditable FROM src AS s
WHERE NOT EXISTS (SELECT 1 FROM mst.SystemSetting AS x WHERE x.SettingKey = s.SettingKey);
GO
