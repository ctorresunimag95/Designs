using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace Microsoft.Extensions.Hosting;

// Keycloak puts realm roles in a nested claim: realm_access.roles = ["api-reader", ...]
// Azure AD puts roles in a flat array:           roles = ["api-reader", ...]
// This transformation copies Keycloak roles into flat "roles" claims so that
// RequireRole / policy.RequireRole("api-reader") work identically against both providers.
// When realm_access is absent (Azure AD tokens), the principal passes through unchanged.
internal sealed class KeycloakClaimsTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var realmAccess = principal.FindFirst("realm_access")?.Value;
        if (realmAccess is null)
            return Task.FromResult(principal);

        using var doc = JsonDocument.Parse(realmAccess);
        if (!doc.RootElement.TryGetProperty("roles", out var rolesElement))
            return Task.FromResult(principal);

        var identity = (ClaimsIdentity)principal.Identity!;
        foreach (var role in rolesElement.EnumerateArray())
        {
            var roleName = role.GetString();
            if (roleName is not null && !identity.HasClaim("roles", roleName))
                identity.AddClaim(new Claim("roles", roleName));
        }

        return Task.FromResult(principal);
    }
}
