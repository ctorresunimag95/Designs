using Azure.Core;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace AspireTemplate.SampleApi.KeyVault;

internal static class KeyVaultConfigurationExtensions
{

    public static IHostApplicationBuilder UseAzureKeyVault<T>(this T hostBuilder)
            where T : IHostApplicationBuilder
    {
        var keyVaultUrl = hostBuilder.Configuration["KeyVault:Url"];

        if (string.IsNullOrWhiteSpace(keyVaultUrl))
        {
            return hostBuilder;
        }

        var secretManager = GetKeyVaultSecretManager(hostBuilder.Environment.EnvironmentName);

        // Client Id will be automatically resolved from the environment variable AZURE_CLIENT_ID if it is set, otherwise it will use the managed identity
        hostBuilder.Configuration.AddAzureKeyVault(new Uri(keyVaultUrl), new DefaultAzureCredential(), secretManager);

        return hostBuilder;
    }

    private static KeyVaultSecretManager GetKeyVaultSecretManager(string environment)
    {
        return new KeyVaultSecretManager();

        // If you want to customize the prefix for the secrets in Key Vault by environment for example, you can do it like this:
        // return new PrefixKeyVaultSecretManager(environment);

        // Secrets in Key Vault can be named with a prefix for the environment, for example:
        // - dev-MySecret
        // - qa-MySecret
        // For nested configuration, you can use double dashes to represent the configuration path delimiter, for example:
        // - dev-MySection--MySecret
    }
}

public class PrefixKeyVaultSecretManager : KeyVaultSecretManager
{
    private readonly string _prefix;

    public PrefixKeyVaultSecretManager(string prefix)
    {
        _prefix = $"{prefix}-";
    }

    public override bool Load(SecretProperties secret) => secret.Name.StartsWith(_prefix);

    public override string GetKey(KeyVaultSecret secret) => secret.Name
        .Substring(_prefix.Length)
        .Replace("--", ConfigurationPath.KeyDelimiter);
}