namespace ProCargo.Application.Common;

/// <summary>Row returned by procedures that create a record: SELECT @Id AS Id.</summary>
public sealed class IdResult
{
    public long Id { get; init; }
}

/// <summary>Row returned by procedures that create a numbered record: SELECT @Id AS Id, @Number AS Number.</summary>
public sealed class CreatedResult
{
    public long Id { get; init; }
    public string Number { get; init; } = string.Empty;
}

/// <summary>Idempotent create/update result: IsExisting = 1 when the request was a replay.</summary>
public sealed class IdempotentResult
{
    public long Id { get; init; }
    public string? Number { get; init; }
    public bool IsExisting { get; init; }
}

/// <summary>Idempotent status update result (no business number).</summary>
public sealed class IdWithFlagResult
{
    public long Id { get; init; }
    public bool IsExisting { get; init; }
}

/// <summary>Response body of POST endpoints that create a numbered business record.</summary>
public sealed record CreatedResponse(long Id, string? Number);

/// <summary>History row shared by booking / trip / quotation status history procedures.</summary>
public sealed class StatusHistoryDto
{
    public long HistoryId { get; init; }
    public int? FromStatusId { get; init; }
    public int ToStatusId { get; init; }
    public string? Remarks { get; init; }
    public long? ChangedBy { get; init; }
    public string? ChangedByName { get; init; }
    public DateTime ChangedDateUtc { get; init; }
}

/// <summary>A file streamed back to the client.</summary>
public sealed record FileDownload(Stream Content, string ContentType, string FileName);
