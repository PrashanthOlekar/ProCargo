using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using ProCargo.Domain.Constants;
using ProCargo.Domain.Enums;

namespace ProCargo.API.Authorization;

/// <summary>
/// [HasPermission(Permissions.ViewBookings)] - the endpoint requires that permission claim. Internal permissions
/// are honoured only in Operations-portal tokens and external permissions only in Web-portal tokens, so a stolen
/// customer token can never reach a staff endpoint even if a role was misconfigured.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "perm:";

    public HasPermissionAttribute(string permission) : base(PolicyPrefix + permission)
    {
        Permission = permission;
    }

    public string Permission { get; }
}

/// <summary>Any one of several permissions (e.g. staff ViewTrips OR driver PerformTrips).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasAnyPermissionAttribute : AuthorizeAttribute
{
    public HasAnyPermissionAttribute(params string[] permissions) : base(HasPermissionAttribute.PolicyPrefix + string.Join('|', permissions))
    {
    }
}

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(IReadOnlyList<string> permissions)
    {
        Permissions = permissions;
    }

    public IReadOnlyList<string> Permissions { get; }
}

public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var portal = context.User.FindFirst(ProCargoClaimTypes.Portal)?.Value;
        foreach (var permission in requirement.Permissions)
        {
            var expectedPortal = Permissions.IsInternal(permission) ? nameof(PortalType.Operations) : nameof(PortalType.Web);
            if (portal == expectedPortal && context.User.HasClaim(ProCargoClaimTypes.Permission, permission))
            {
                context.Succeed(requirement);
                break;
            }
        }

        return Task.CompletedTask;
    }
}

/// <summary>Builds "perm:X" / "perm:X|Y" policies on demand so every permission does not need registering.</summary>
public sealed class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : base(options)
    {
    }

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(HasPermissionAttribute.PolicyPrefix, StringComparison.Ordinal))
        {
            var permissions = policyName[HasPermissionAttribute.PolicyPrefix.Length..].Split('|', StringSplitOptions.RemoveEmptyEntries);
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permissions))
                .Build();
        }

        return await base.GetPolicyAsync(policyName);
    }
}

public static class Policies
{
    /// <summary>Signed in through the Operations portal (internal staff).</summary>
    public const string Staff = "Staff";

    /// <summary>Signed in through the customer / partner website.</summary>
    public const string External = "External";
}
