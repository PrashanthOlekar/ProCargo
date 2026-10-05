namespace ProCargo.Domain.Exceptions;

/// <summary>Base type for violations of a business invariant detected in the domain layer.</summary>
public class DomainException : Exception
{
    public DomainException(string errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}

/// <summary>Thrown when a status change is not allowed by the entity's state machine.</summary>
public sealed class InvalidStatusTransitionException : DomainException
{
    public InvalidStatusTransitionException(string entity, string from, string to)
        : base($"{entity.ToUpperInvariant()}_INVALID_TRANSITION", $"{entity} cannot move from '{from}' to '{to}'.")
    {
        Entity = entity;
        From = from;
        To = to;
    }

    public string Entity { get; }
    public string From { get; }
    public string To { get; }
}
