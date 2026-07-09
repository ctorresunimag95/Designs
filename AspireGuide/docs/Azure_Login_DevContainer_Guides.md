# Azure Login & Key Vault from a Dev Container

This guide explains when you might need Key Vault access from a dev container and outlines two authentication options:

1. `az login` for interactive browser-based sign-in, with `az login --use-device-code` as a fallback.
2. A service principal, recommended when interactive sign-in is not available.

## When You Might Need This

- Running integration tests or debugging code that reads secrets directly from Key Vault.
- Validating Key Vault access policies or RBAC during development.
- Performing short-lived troubleshooting that requires a real secret value.

Important: avoid connecting local development directly to a production Key Vault when possible. Prefer mocks, local configuration, or a dedicated development vault with limited privileges.

## Option 1: Azure CLI Interactive Login

Preferred: run interactive `az login` inside the dev container. This opens a browser for authentication and uses your user identity.

```bash
az login
az account show
az account get-access-token --resource https://vault.azure.net --output json
```

If browser-based sign-in is not possible from the container, use device code instead. This lets you complete authentication from another device or browser session.

```bash
az login --use-device-code
az account show
az account get-access-token --resource https://vault.azure.net --output json
```

- Pros: uses your existing user identity and permissions, and is quick to set up for ad hoc testing.
- Cons: both flows may be blocked by corporate SSO or policy; interactive sign-in also requires browser and network access.

## Option 2: Service Principal

Use a service principal when interactive sign-in is unavailable.

Create the service principal in an environment where you have permission to do so:

```bash
az ad sp create-for-rbac --name "dev-my-sp" --role Reader \
  --scopes /subscriptions/<SUBSCRIPTION_ID> \
  --sdk-auth
```

The command returns JSON that includes `clientId`, `clientSecret`, `tenantId`, and `subscriptionId`.

Add these values to `.devcontainer/devcontainer.json` as container environment variables, and do not commit secrets:

```json
"containerEnv": {
  "AZURE_CLIENT_ID": "<client-id>",
  "AZURE_TENANT_ID": "<tenant-id>",
  "AZURE_CLIENT_SECRET": "<client-secret>",
  "AZURE_SUBSCRIPTION_ID": "<subscription-id>"
}
```

`DefaultAzureCredential` uses `EnvironmentCredential` when these environment variables are present.

Make sure the service principal has Key Vault access through either RBAC or access policies for the vault you need to reach.

## Rebuild the Dev Container

- In VS Code, run **Dev Containers: Rebuild and Reopen in Container** from the Command Palette.
- From the CLI, run `devcontainer rebuild --workspace-folder .`.

## Verify Access Inside the Container

```bash
az --version
az account show
az account get-access-token --resource https://vault.azure.net --output json
```

## Additional Notes

- If you previously mounted host `~/.azure` into the container, note that this was removed. The service principal approach does not require that mount.
- Do not commit secrets to source control. Use VS Code secret storage, dev container secrets, or CI secret injection instead.
- For workloads running in Azure, such as App Service or AKS, prefer managed identities over service principals.

## Security Reminder

- Use short-lived credentials when possible, and rotate secrets regularly.
