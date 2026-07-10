# Key Vault Configuration Guide

Use this guide to load secrets from an Azure Key Vault into .NET configuration while keeping the vault optional for local development.

## 1. Configure the vault URL

```bash
dotnet user-secrets set "Parameters:KeyVaultUrl" "https://<vault-name>.vault.azure.net/" --project infrastructure/AspireGuide.AppHost
```

The AppHost passes the value to the API as `KeyVault__Url`. Leave the parameter empty when the API should run without Key Vault.

Or just set the value in the appsettings file for local development:

```json
{
  "Parameters": {
    "KeyVaultUrl": "https://<vault-name>.vault.azure.net/"
  }
}
```

## 2. Authenticate locally

```bash
az login
az account set --subscription "<SUBSCRIPTION_ID_OR_NAME>"
az account get-access-token --resource https://vault.azure.net --output json
```

The API uses `DefaultAzureCredential`, which can discover the Azure CLI credential.

## 3. Grant secret read access

Grant the local developer identity or deployed managed identity the smallest required role, commonly `Key Vault Secrets User`:

```bash
az role assignment create \
  --assignee "<OBJECT_ID_OR_CLIENT_ID>" \
  --role "Key Vault Secrets User" \
  --scope "/subscriptions/<SUBSCRIPTION_ID>/resourceGroups/<RESOURCE_GROUP>/providers/Microsoft.KeyVault/vaults/<VAULT_NAME>"
```

Use the identity object ID where possible for unambiguous role assignment. Confirm that the vault uses RBAC or adapt the permission setup if it uses legacy access policies.

## 4. Add and read a secret

```bash
az keyvault secret set \
  --vault-name "<VAULT_NAME>" \
  --name "Test" \
  --value "development-only-value"
```

The Key Vault configuration provider maps secret names into .NET configuration. The sample API reads the `Test` configuration key at `GET /api/read-config`.

## 5. Use managed identity in Azure

For a deployed workload, assign a system-assigned or user-assigned managed identity to the workload and grant that identity `Key Vault Secrets User`. Do not create or copy a client secret for a managed identity. If multiple user-assigned identities are attached, configure the identity client ID used by `DefaultAzureCredential`.

Never commit secret values, tokens, or exported credentials.
