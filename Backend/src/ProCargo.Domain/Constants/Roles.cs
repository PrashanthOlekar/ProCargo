namespace ProCargo.Domain.Constants;

/// <summary>
/// System role names (seeded in sec.Role). New roles can be created at runtime; code only
/// relies on these names for "which portal / which profile" decisions, never for permissions.
/// </summary>
public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string Operations = "Operations";
    public const string Finance = "Finance";
    public const string Support = "Support";
    public const string Customer = "Customer";
    public const string VehicleOwner = "VehicleOwner";
    public const string Driver = "Driver";

    public static readonly IReadOnlyCollection<string> External = [Customer, VehicleOwner, Driver];
    public static readonly IReadOnlyCollection<string> Internal = [SuperAdmin, Admin, Operations, Finance, Support];
}
