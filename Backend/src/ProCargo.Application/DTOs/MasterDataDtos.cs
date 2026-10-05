namespace ProCargo.Application.DTOs;

/// <summary>mst.usp_Lookup_GetAll</summary>
public sealed class LookupItemDto
{
    public string LookupType { get; init; } = string.Empty;
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public bool IsTerminal { get; init; }
}

public sealed class StateDto
{
    public int StateId { get; init; }
    public string StateCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

public sealed class CityDto
{
    public int CityId { get; init; }
    public int StateId { get; init; }
    public string StateName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

public sealed class VehicleTypeDto
{
    public int VehicleTypeId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal CapacityKg { get; init; }
    public decimal? LengthFt { get; init; }
    public int SortOrder { get; init; }
    public bool IsActive { get; init; }
}

public sealed class GoodsTypeDto
{
    public int GoodsTypeId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool RequiresSpecialHandling { get; init; }
    public bool IsActive { get; init; }
}

public sealed class DocumentTypeDto
{
    public int DocumentTypeId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string AppliesTo { get; init; } = string.Empty;
    public bool RequiresExpiry { get; init; }
    public bool IsMandatory { get; init; }
    public bool IsActive { get; init; }
}

public sealed class SystemSettingDto
{
    public string SettingKey { get; init; } = string.Empty;
    public string SettingValue { get; init; } = string.Empty;
    public string DataType { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsEditable { get; init; }
    public DateTime? ModifiedDateUtc { get; init; }
}

/// <summary>Everything the front-ends need for dropdowns and status labels, in one cached call.</summary>
public sealed record ReferenceDataResponse(
    IReadOnlyDictionary<string, IReadOnlyList<LookupItemDto>> Lookups,
    IReadOnlyList<VehicleTypeDto> VehicleTypes,
    IReadOnlyList<GoodsTypeDto> GoodsTypes,
    IReadOnlyList<StateDto> States);
