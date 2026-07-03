 # Azure Login & Key Vault from the devcontainer

 This doc explains why you might need Key Vault access from a devcontainer and two approaches to authenticate:
 1. `az login` (device-code) — use if your organization allows interactive/device flows.
 2. Service Principal (recommended when interactive login is not possible).

 Why you might need this
 - Running integration tests or debugging code that reads secrets directly from Key Vault.
 - Validating Key Vault access policies or RBAC in a development scenario.
 - Short-lived troubleshooting that requires a real secret value.

 Important: prefer not to connect local development directly to production Key Vault. Use mocks, local config, or a dedicated dev vault with limited privileges.

 Option 1 — Azure CLI interactive login (preferred) and device-code fallback
 - Preferred: run the interactive `az login` inside the devcontainer — this opens a browser for authentication and uses your user identity:

 ```bash
 az login
 az account show
 az account get-access-token --resource https://vault.azure.net --output json
 ```

 - If the interactive/browser flow is not possible from the container, try the device-code flow (falls back to a browser on another device):

 ```bash
 az login --use-device-code
 az account show
 az account get-access-token --resource https://vault.azure.net --output json
 ```

 - Pros: uses your user identity and existing permissions; quick to set up for ad-hoc tests.
 - Cons: both flows may be blocked by corporate SSO/policy; interactive flow requires a browser and network access.

 Option 2 — Service Principal (recommended when interactive login is unavailable)
 - Create an SP (run where you can create it):

 ```bash
 az ad sp create-for-rbac --name "dev-my-sp" --role Reader \
   --scopes /subscriptions/<SUBSCRIPTION_ID> \
   --sdk-auth
 ```

 - The command returns JSON with `clientId`, `clientSecret`, `tenantId`, and `subscriptionId`.
 - Add these to your `.devcontainer/devcontainer.json` as container environment variables (don’t commit secrets):

 ```json
 "containerEnv": {
   "AZURE_CLIENT_ID": "<client-id>",
   "AZURE_TENANT_ID": "<tenant-id>",
   "AZURE_CLIENT_SECRET": "<client-secret>",
   "AZURE_SUBSCRIPTION_ID": "<subscription-id>"
 }
 ```

 - `DefaultAzureCredential` will use `EnvironmentCredential` when these env vars are present.
 - Ensure the SP has Key Vault access (RBAC role or access policies) for the vault you need to contact.

 Rebuild the devcontainer
 - In VS Code: Command Palette → **Dev Containers: Rebuild and Reopen in Container**
 - Or CLI: `devcontainer rebuild --workspace-folder .`

 Quick verification inside the container
 ```bash
 az --version
 az account show
 az account get-access-token --resource https://vault.azure.net --output json
 ```

 Additional notes
 - If you previously mounted host `~/.azure` into the container, note that this was removed — the SP approach does not need that mount.
 - Do not commit secrets to source control. Use VS Code secret storage, devcontainer secrets, or CI secret injection.
 - For anything running in Azure (App Service, AKS, etc.), prefer managed identities instead of SPs.

 Security reminder
 - Use short-lived credentials where possible and rotate secrets regularly.
