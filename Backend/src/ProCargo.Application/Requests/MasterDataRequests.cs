namespace ProCargo.Application.Requests;

public sealed class SaveStateRequest
{
    public string StateCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class SaveCityRequest
{
    public int StateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class SaveVehicleTypeRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal CapacityKg { get; set; }
    public decimal? LengthFt { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class SaveGoodsTypeRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool RequiresSpecialHandling { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class SaveDocumentTypeRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AppliesTo { get; set; } = string.Empty;
    public bool RequiresExpiry { get; set; }
    public bool IsMandatory { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UpdateSystemSettingRequest
{
    public string Value { get; set; } = string.Empty;
}
