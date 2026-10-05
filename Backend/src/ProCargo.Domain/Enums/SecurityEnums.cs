namespace ProCargo.Domain.Enums;

/// <summary>
/// The front-end a token was issued for. Web = ProCargo.UI (customers, owners, drivers),
/// Operations = ProCargo.Operations (internal staff). Tokens are not interchangeable.
/// </summary>
public enum PortalType
{
    Web = 1,
    Operations = 2
}

/// <summary>Self-service account types available on the public registration page.</summary>
public enum AccountType
{
    Customer = 1,
    VehicleOwner = 2,
    Driver = 3
}
