using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

public static class AuthenticationExtensions
{
    /// <summary>
    /// Registers JWT Bearer authentication. Supports two local dev providers — only the config differs:
    ///
    ///   Keycloak (Option A) — set Authentication:Authority (injected by AppHost via WithKeycloakAuthentication):
    ///     Local  : http://localhost:8080/realms/aspire-guide  | Audience = sample-api
    ///     Azure AD: https://login.microsoftonline.com/{tid}/v2.0 | Audience = api://{clientId}
    ///
    ///   dotnet user-jwts (Option B) — no Authority needed; run "Generate Dev Token" in the Aspire dashboard.
    ///     The tool writes Authentication:Schemes:Bearer:ValidIssuer into appsettings.Development.json
    ///     and the JwtBearer middleware picks it up automatically.
    ///
    /// Silently skips registration when neither provider is configured (e.g. standalone test runs).
    /// </summary>
    public static TBuilder AddApiAuthentication<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var authority = builder.Configuration["Authentication:Authority"];
        var validIssuer = builder.Configuration["Authentication:Schemes:Bearer:ValidIssuer"];

        if (string.IsNullOrWhiteSpace(authority) && string.IsNullOrWhiteSpace(validIssuer))
            return builder;

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                if (!string.IsNullOrWhiteSpace(authority))
                {
                    // Option A: Keycloak / Azure AD — OIDC discovery from Authority endpoint.
                    // Keycloak runs locally over plain HTTP; Azure AD is always HTTPS.
                    options.Authority = authority;
                    options.Audience = builder.Configuration["Authentication:Audience"] ?? "sample-api";
                    options.RequireHttpsMetadata = !authority.StartsWith("http://", StringComparison.OrdinalIgnoreCase);

                    // appsettings.Development.json pre-populates ValidIssuer for Option B.
                    // Clear it here so Keycloak OIDC discovery is the sole issuer authority
                    // and user-jwts tokens cannot be accepted in this mode.
                    options.TokenValidationParameters.ValidIssuer = null;
                    options.TokenValidationParameters.ValidIssuers = null;
                }
                // Option B: dotnet user-jwts — no Authority; JwtBearer reads ValidIssuer +
                // ValidAudiences from Authentication:Schemes:Bearer (appsettings.Development.json).

                options.MapInboundClaims = false;
                // Mutate rather than replace so the config-bound values from Option B are preserved.
                options.TokenValidationParameters.RoleClaimType = "roles";
            });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("ApiReader", policy => policy.RequireRole("api-reader"));
            options.AddPolicy("ApiWriter", policy => policy.RequireRole("api-writer"));
        });

        // Normalises Keycloak's nested realm_access.roles into flat roles claims to match
        // Azure AD's token structure. Is a no-op for Azure AD tokens.
        builder.Services.AddScoped<IClaimsTransformation, KeycloakClaimsTransformation>();

        return builder;
    }
}
