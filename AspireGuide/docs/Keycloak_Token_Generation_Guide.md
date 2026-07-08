# Keycloak Token Generation Guide

This guide explains how to generate access tokens from the local Keycloak instance for both user authentication and machine-to-machine communication.

## Prerequisites

- AspireGuide AppHost is running (`dotnet run --project infrastructure/AspireGuide.AppHost`)
- Keycloak is accessible at `http://localhost:8080`
- Realm: `aspire-guide`

---

## Machine-to-Machine (M2M) — Client Credentials Flow

Use this flow for service-to-service authentication where no user is involved.

### Configuration
- **Client ID**: `sample-api-m2m`
- **Client Secret**: `local-dev-secret`
- **Grant Type**: `client_credentials`

### curl Command

```bash
curl -X POST http://localhost:8080/realms/aspire-guide/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id=sample-api-m2m" \
  -d "client_secret=local-dev-secret"
```

### Response

```json
{
  "access_token": "eyJhbGciOiJSUzI1NiIsInR5cC...",
  "expires_in": 900,
  "token_type": "Bearer"
}
```

### Using the Token

```bash
TOKEN="<access_token_from_response>"

curl http://localhost:{api-port}/api/secure/files \
  -H "Authorization: Bearer $TOKEN"
```

---

## Normal User — Resource Owner Password Flow

Use this flow for interactive applications where a user provides their username and password directly.

### Test Users

| Username | Password | Roles |
|----------|----------|-------|
| `dev-user` | `Dev@1234` | `api-reader` |
| `dev-admin` | `DevAdmin@1234` | `api-reader`, `api-writer` |

### Configuration
- **Client ID**: `sample-api-ui`
- **Grant Type**: `password`

### curl Command — dev-user

```bash
curl -X POST http://localhost:8080/realms/aspire-guide/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password" \
  -d "client_id=sample-api-ui" \
  -d "username=dev-user" \
  -d "password=Dev@1234" \
  -d "scope=openid"
```

### curl Command — dev-admin (higher privileges)

```bash
curl -X POST http://localhost:8080/realms/aspire-guide/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password" \
  -d "client_id=sample-api-ui" \
  -d "username=dev-admin" \
  -d "password=DevAdmin@1234" \
  -d "scope=openid"
```

### Response

```json
{
  "access_token": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expires_in": 900,
  "refresh_token": "eyJhbGciOiJSUzI1NiIsInR5cCIsInR5cCI6IkpXVCJ9...",
  "token_type": "Bearer",
  "scope": "openid"
}
```

### Using the Token

```bash
TOKEN="<access_token_from_response>"

# Test the token — returns all claims
curl http://localhost:{api-port}/api/secure/whoami \
  -H "Authorization: Bearer $TOKEN"

# Call protected endpoint that requires api-reader role
curl http://localhost:{api-port}/api/secure/files \
  -H "Authorization: Bearer $TOKEN"
```

---

## Debugging Token Content

The `/api/secure/whoami` endpoint echoes all claims in the token, useful for verifying roles and permissions:

```bash
curl http://localhost:{api-port}/api/secure/whoami \
  -H "Authorization: Bearer $TOKEN"
```

Example response:
```json
[
  { "type": "sub", "value": "12345678-1234-1234-1234-123456789012" },
  { "type": "roles", "value": "api-reader" },
  { "type": "aud", "value": "sample-api" },
  { "type": "preferred_username", "value": "dev-user" }
]
```

---

## Extending Test Users and Clients

The sample users (`dev-user`, `dev-admin`) and clients (`sample-api-ui`, `sample-api-m2m`) are loaded from the local realm configuration file at startup.

### File Location
```
infrastructure/AspireGuide.AppHost/Keycloak/realm-export.json
```

### Adding More Users

Edit `realm-export.json` and add entries to the `users` array:

```json
{
  "username": "custom-user",
  "enabled": true,
  "emailVerified": true,
  "email": "custom@local.dev",
  "requiredActions": [],
  "credentials": [
    {
      "type": "password",
      "value": "Custom@1234",
      "temporary": false
    }
  ],
  "realmRoles": [ "api-reader" ]
}
```

### Adding More Clients

Edit `realm-export.json` and add entries to the `clients` array:

```json
{
  "clientId": "custom-client",
  "publicClient": false,
  "clientAuthenticatorType": "client-secret",
  "secret": "custom-secret-key",
  "serviceAccountsEnabled": true,
  "standardFlowEnabled": false,
  "directAccessGrantsEnabled": false,
  "protocolMappers": [
    {
      "name": "audience-mapper",
      "protocol": "openid-connect",
      "protocolMapper": "oidc-audience-mapper",
      "consentRequired": false,
      "config": {
        "included.custom.audience": "sample-api",
        "access.token.claim": "true",
        "id.token.claim": "false"
      }
    }
  ]
}
```

### Reloading Configuration

After modifying `realm-export.json`:

1. Stop the AppHost
2. Delete the Keycloak volume to force a fresh import:
   ```bash
   docker volume rm keycloak-data
   ```
3. Restart the AppHost — Keycloak will reimport the realm with your changes

---

## Switching to Real Azure AD

To use this with real Azure AD instead of local Keycloak, only the environment variables change — **no code modifications required**:

```bash
export Authentication__Authority=https://login.microsoftonline.com/{tenantId}/v2.0
export Authentication__Audience=api://{clientId}
```

The same token endpoints and JWT Bearer validation work identically.

## References

- [Keycloak Documentation](https://www.keycloak.org/documentation)
- [Aspire Keycloak Integration Guide](https://aspire.dev/integrations/security/keycloak/)
- [OAuth 2.0 and OpenID Connect](https://oauth.net/2/)