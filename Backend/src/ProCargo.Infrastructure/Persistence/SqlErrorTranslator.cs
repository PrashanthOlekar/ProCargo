using Microsoft.Data.SqlClient;
using ProCargo.Application.Common;
using ProCargo.Application.Exceptions;

namespace ProCargo.Infrastructure.Persistence;

/// <summary>
/// Maps SQL Server errors to application exceptions.
///   504xx  raised by our procedures with "ERROR_CODE|message"
///   2601/2627 unique index / constraint violation -> 409
///   547    foreign key / check constraint violation -> 422
/// Anything else is left untouched and surfaces as a 500 (details are logged, never returned).
/// </summary>
internal static class SqlErrorTranslator
{
    public static bool TryTranslate(Exception exception, out Exception? translated)
    {
        translated = null;
        var sql = Find(exception);
        if (sql is null) return false;

        var (code, message) = Parse(sql.Message);

        translated = sql.Number switch
        {
            50400 => new BusinessRuleException(code ?? "BUSINESS_RULE_VIOLATION", message),
            50403 => new ForbiddenException(message),
            50404 => new NotFoundException(code ?? ErrorCodes.NotFound, message),
            50409 => new ConflictException(code ?? ErrorCodes.Conflict, message),
            50412 => new ConcurrencyException(message),
            2601 or 2627 => new ConflictException(ErrorCodes.DuplicateValue, "A record with the same unique value already exists."),
            547 => new BusinessRuleException(ErrorCodes.ReferenceInvalid,
                "The request references a record that does not exist, or violates a data rule."),
            _ => null
        };

        return translated is not null;
    }

    private static SqlException? Find(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is SqlException sql) return sql;
            exception = exception.InnerException;
        }

        return null;
    }

    private static (string? Code, string Message) Parse(string raw)
    {
        var separator = raw.IndexOf('|', StringComparison.Ordinal);
        if (separator <= 0) return (null, raw);
        return (raw[..separator].Trim(), raw[(separator + 1)..].Trim());
    }
}
