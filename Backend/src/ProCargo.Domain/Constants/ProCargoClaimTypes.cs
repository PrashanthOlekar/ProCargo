namespace ProCargo.Domain.Constants;

/// <summary>Custom JWT claim names issued by the API.</summary>
public static class ProCargoClaimTypes
{
    public const string UserId = "sub";
    public const string Permission = "perm";
    public const string Role = "role";
    public const string Portal = "portal";
    public const string CustomerId = "cid";
    public const string OwnerId = "oid";
    public const string DriverId = "did";
    public const string MustChangePassword = "mcp";
}
