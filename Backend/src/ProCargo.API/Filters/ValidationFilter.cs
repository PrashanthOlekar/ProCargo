using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using ProCargo.Application.Exceptions;

namespace ProCargo.API.Filters;

/// <summary>
/// Runs the FluentValidation validator registered for every action argument before the action executes.
/// Validation errors become a 400 with field-level messages (see ExceptionHandlingMiddleware).
/// </summary>
public sealed class ValidationFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _services;

    public ValidationFilter(IServiceProvider services)
    {
        _services = services;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null || argument is CancellationToken || argument.GetType().IsPrimitive || argument is string) continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (_services.GetService(validatorType) is not IValidator validator) continue;

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            if (!result.IsValid)
            {
                var errors = result.Errors
                    .GroupBy(e => ToCamelCase(e.PropertyName))
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
                throw new RequestValidationException(errors);
            }
        }

        await next();
    }

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) ? "request" : string.Join('.', name.Split('.').Select(p => p.Length > 0 ? char.ToLowerInvariant(p[0]) + p[1..] : p));
}
