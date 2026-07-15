# Keycloak Authentication Configuration Guide

Use this guide to configure Keycloak for local development, connect it to a .NET Aspire AppHost, and test user and service-to-service authentication.

For the complete Aspire integration API, see [Keycloak integration | Aspire](https://aspire.dev/integrations/security/keycloak/). For running Keycloak directly in Docker, see [Docker - Keycloak](https://www.keycloak.org/getting-started/getting-started-docker).

## 1. Choose how to run Keycloak

Use the Aspire-managed container when working with this repository. Use the standalone Docker command when testing Keycloak independently.

### Option A: Run through Aspire

The repository registers Keycloak with a stable port, persistent data volume, realm import, and OTLP exporter:

```csharp
var keycloak = builder.AddKeycloak("keycloak", port: 8080)
    .WithOtlpExporter()
    .WithRealmImport(Path.Combine(AppContext.BaseDirectory, "Keycloak"))
    .WithDataVolume("keycloak-data")
    .WithLifetime(ContainerLifetime.Persistent);
```

Start the AppHost from the repository root:

```bash
dotnet run --project infrastructure/AspireTemplate.AppHost
```

The application realm is available at:

```text
http://localhost:8080/realms/aspire-guide
```

A stable port is useful for local OIDC clients because browser cookies and redirect configuration can persist between AppHost runs.

### Option B: Run Keycloak directly with Docker

Use this command when the Aspire AppHost is not required:

```bash
docker run --name keycloak -p 127.0.0.1:8080:8080 \
  -e KC_BOOTSTRAP_ADMIN_USERNAME=admin \
  -e KC_BOOTSTRAP_ADMIN_PASSWORD=admin \
  quay.io/keycloak/keycloak:26.7.0 start-dev
```

Open the [Keycloak Admin Console](http://localhost:8080/admin) and sign in with the bootstrap administrator credentials. Change development credentials before using the instance for anything beyond local testing.

## 2. Understand the repository realm

The Aspire AppHost imports the realm from:

```text
infrastructure/AspireTemplate.AppHost/Keycloak/realm-export.json
```

The imported realm is `aspire-guide` and contains:

| Client or user | Type | Purpose |
| --- | --- | --- |
| `sample-api-ui` | Public client | Development user login with direct access grants |
| `sample-api-m2m` | Confidential client | Client credentials flow |
| `dev-user` | Test user | `api-reader` role |
| `dev-admin` | Test user | `api-reader` and `api-writer` roles |

The AppHost injects the authority into the API as `Authentication__Authority` and waits for Keycloak before starting the API.

## 3. Add or modify a realm configuration

For local development, edit the `users`, `clients`, or roles in `realm-export.json`. Keep test passwords and client secrets local-only.

After changing the import file:

1. Stop the AppHost.
2. Remove the persistent volume so Keycloak does not reuse the old realm:

   ```bash
   docker volume rm keycloak-data
   ```

3. Start the AppHost again.
4. Confirm the realm and clients in the Admin Console.

For a standalone Docker container, create a realm, user, and client through the Admin Console by following the [official Keycloak Docker guide](https://www.keycloak.org/getting-started/getting-started-docker).

## 4. Generate a machine-to-machine token

Use the confidential `sample-api-m2m` client for service-to-service testing:

```bash
curl -X POST http://localhost:8080/realms/aspire-guide/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id=sample-api-m2m" \
  -d "client_secret=local-dev-secret"
```

Copy the returned `access_token` and call a protected endpoint:

```bash
curl http://localhost:<api-port>/api/secure/files \
  -H "Authorization: Bearer <access-token>"
```

Use client credentials only for non-user workloads. Never use the local client secret in a production environment.

## 5. Generate a development user token

For local testing, request a token with the public `sample-api-ui` client. The password grant is enabled only as a development convenience:

```bash
curl -X POST http://localhost:8080/realms/aspire-guide/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password" \
  -d "client_id=sample-api-ui" \
  -d "username=dev-user" \
  -d "password=Dev@1234" \
  -d "scope=openid"
```

Use `dev-admin` to test the additional `api-writer` role:

```text
Username: dev-admin
Password: DevAdmin@1234
```

Test the token and authorization:

```bash
curl http://localhost:<api-port>/api/secure/whoami \
  -H "Authorization: Bearer <access-token>"

curl http://localhost:<api-port>/api/secure/files \
  -H "Authorization: Bearer <access-token>"
```

## 6. Verify the API authority and audience

The API validates JWTs from the configured authority. Inspect the token claims with `/api/secure/whoami` and confirm that the issuer, audience, subject, and roles match the expected client and realm.

For production, configure an HTTPS authority explicitly and keep HTTPS metadata validation enabled. Follow the [Aspire Keycloak client integration guidance](https://aspire.dev/integrations/security/keycloak/) when using Aspire's Keycloak authentication packages instead of the repository's custom environment-based configuration.

## 7. Move from local Keycloak to Microsoft Entra ID

Provide the production authority and audience through deployment configuration rather than changing endpoint authorization code:

```bash
Authentication__Authority=https://login.microsoftonline.com/<tenant-id>/v2.0
Authentication__Audience=api://<client-id>
```

Use secure deployment configuration, HTTPS, managed identities where applicable, and production identity-provider policies. Do not import the local realm passwords or client secrets into Azure.

## References

- [Keycloak integration | Aspire](https://aspire.dev/integrations/security/keycloak/)
- [Docker - Keycloak](https://www.keycloak.org/getting-started/getting-started-docker)
- [Keycloak documentation](https://www.keycloak.org/documentation)
