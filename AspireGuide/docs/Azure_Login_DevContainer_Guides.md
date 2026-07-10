# Azure CLI Authentication from a Dev Container

Use this guide when local code uses `DefaultAzureCredential` and needs Azure CLI credentials to access Azure resources. This is the local-development equivalent of a managed identity. A user-assigned managed identity itself is normally used by an Azure-hosted workload; it is not a credential that can be used directly by `az login`.

## 1. Install and verify the Azure CLI

Inside the dev container, verify that the CLI is available:

```bash
az --version
```

If the command is unavailable, add the Azure CLI installation to the dev container definition before continuing.

## 2. Sign in

Use browser authentication when the container can open or share a browser:

```bash
az login
```

If the container cannot open a browser, use device code authentication:

```bash
az login --use-device-code
```

Complete the sign-in in a browser on another device when prompted.

## 3. Select the tenant and subscription

```bash
az account list --output table
az account set --subscription "<SUBSCRIPTION_ID_OR_NAME>"
az account show --output table
```

If the resource belongs to another tenant, sign in with the tenant explicitly:

```bash
az login --tenant "<TENANT_ID>"
```

## 4. Verify the credential used by the application

Request an access token for a representative resource:

```bash
az account get-access-token --resource https://management.azure.com/ --output json
```

For Key Vault, use:

```bash
az account get-access-token --resource https://vault.azure.net --output json
```

If these commands succeed, `DefaultAzureCredential` can usually discover the Azure CLI credential inside the same container. The signed-in identity must still have the required data-plane role on the target resource.

## 5. Grant the required permissions

Ask an administrator to grant the signed-in user only the role required by the workload. Examples include `Key Vault Secrets User`, `Storage Blob Data Reader`, and `Azure Service Bus Data Receiver`.

Management roles such as `Reader` do not automatically grant permission to read data from a vault, storage account, or Service Bus namespace.

## 6. Use a user-assigned managed identity in Azure

For a deployed App Service, Function App, VM, or container workload:

1. Create or select a user-assigned managed identity.
2. Assign that identity to the Azure workload.
3. Grant the identity the required data-plane roles.
4. Configure the application to use the identity client ID when more than one identity is available.

For local development, continue using `az login` and `DefaultAzureCredential`; do not copy managed-identity secrets because managed identities do not have client secrets.

## Troubleshooting

- Run `az account show` to confirm the active tenant and subscription.
- Run `az login --tenant "<TENANT_ID>"` if the identity exists in another tenant.
- Confirm the role assignment targets the correct resource scope and identity.
- If a dev container was rebuilt, sign in again because the Azure CLI token cache may not be persisted.
- Do not mount a production host Azure profile into a container unless the security implications are understood.

Never commit tokens or credentials. Prefer short-lived Azure CLI sessions locally and managed identities for deployed workloads.
