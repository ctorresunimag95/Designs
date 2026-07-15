using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Extensions.Hosting;

public static class AuthenticationExtensions
{
    /// <summary>
    /// Registers JWT Bearer authentication configured from Authentication:Authority and Authentication:Audience.
    /// Silently skips registration when Authority is not configured (e.g. standalone test runs).
    ///
    /// Works with both Keycloak (local) and Azure AD (production) — only the config values differ:
    ///   Local Keycloak : Authority = http://localhost:8080/realms/aspire-guide  | Audience = sample-api
    ///   Azure AD       : Authority = https://login.microsoftonline.com/{tid}/v2.0 | Audience = api://{clientId}
    /// </summary>
    public static TBuilder AddApiAuthentication<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var authority = builder.Configuration["Authentication:Authority"];
        var audience = builder.Configuration["Authentication:Audience"] ?? "sample-api";

        if (string.IsNullOrWhiteSpace(authority))
            return builder;

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = audience;
                options.MapInboundClaims = false;

                // Keycloak runs locally over plain HTTP; Azure AD is always HTTPS.
                // Deriving the flag from the URL prefix avoids a separate config switch.
                options.RequireHttpsMetadata = !authority.StartsWith("http://", StringComparison.OrdinalIgnoreCase);

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = true,
                    RoleClaimType = "roles",
                };
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
