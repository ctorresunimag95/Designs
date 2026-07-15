namespace AspireTemplate.AppHost.Extensions;

internal static class KeycloakExtensions
{
    private const string RealmName = "aspire-guide";

    /// <summary>
    /// Adds a local Keycloak container pre-configured with the aspire-guide realm.
    /// The realm is imported from Keycloak/realm-export.json and includes two clients
    /// (Auth Code + PKCE and Client Credentials) and two test users.
    /// </summary>
    internal static IResourceBuilder<KeycloakResource> AddLocalKeycloak(
        this IDistributedApplicationBuilder builder)
    {
        return builder.AddKeycloak("keycloak", port: 8080)
            .WithOtlpExporter()
            .WithRealmImport(Path.Combine(AppContext.BaseDirectory, "Keycloak"))
            .WithDataVolume("keycloak-data")
            .WithLifetime(ContainerLifetime.Persistent);
    }

    /// <summary>
    /// Injects the Keycloak realm authority URL into the resource as Authentication__Authority,
    /// so the service can validate JWT tokens without knowing the Keycloak host at build time.
    /// In production, replace this with the real Azure AD authority via environment config.
    /// </summary>
    internal static IResourceBuilder<TResource> WithKeycloakAuthentication<TResource>(
        this IResourceBuilder<TResource> resourceBuilder,
        IResourceBuilder<KeycloakResource> keycloak)
        where TResource : IResourceWithEnvironment
    {
        var endpoint = keycloak.GetEndpoint("http");
        return resourceBuilder.WithEnvironment(
            "Authentication__Authority",
            ReferenceExpression.Create($"{endpoint}/realms/{RealmName}"));
    }
}
