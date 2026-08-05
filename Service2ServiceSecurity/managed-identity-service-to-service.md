# Managed Identity Service-to-Service Communication

## Overview

Secure communication between Azure Functions using managed identities and Entra ID without storing credentials or secrets.

## Architecture Flow

```mermaid
sequenceDiagram
    participant FA as Function A<br/>(Caller)
    participant EntraID as Entra ID<br/>(OAuth)
    participant FB as Function B<br/>(Server)

    FA->>FA: 1. DefaultAzureCredential()<br/>uses managed identity

    FA->>EntraID: 2. Request token<br/>Scope: https://functionb-uri

    EntraID->>EntraID: 3. Validate Function A<br/>identity

    EntraID->>EntraID: 4. Look up role assignment<br/>(Function A → api.reader)

    EntraID->>FA: 5. Return JWT token with:<br/>- oid: function-a-id<br/>- appid: function-a-appid<br/>- roles: ["api.reader"]

    FA->>FB: 6. HTTP request with<br/>Authorization: Bearer {token}

    FB->>FB: 7. Validate token<br/>signature & scope

    FB->>FB: 8. Check roles claim<br/>roles.contains("api.reader")

    FB->>FA: 9. ✅ 200 OK<br/>Access Granted

    Note over FA,FB: Token is cached ~1 hour<br/>Next calls reuse same token
```

## Implementation Steps

### 1. Register Function B as Enterprise Application (if not already)

- Function B needs an Entra ID registration
- Define App Roles in the manifest (e.g., "api.reader", "api.writer")

### 2. Assign Managed Identity to Function A

- System-assigned or User-assigned managed identity

### 3. Configure Role Assignment in Azure Portal

- Go to Function B → Enterprise Application
- Users and groups → Assign role to Function A's managed identity
- Select the appropriate role (e.g., "api.reader")

### 4. Function A Code

```csharp
using Azure.Identity;
using Azure.Core;

var credential = new DefaultAzureCredential();
var token = await credential.GetTokenAsync(
    new TokenRequestContext(new[] { "https://{functionb-uri}/.default" })
);

var client = new HttpClient();
client.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Bearer", token.Token);

var response = await client.GetAsync("https://{functionb-uri}/api/endpoint");
```

### 5. Function B Code (Azure Function)

```csharp
[FunctionName("MySecureFunction")]
public async Task<IActionResult> Run(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
    ILogger log)
{
    // Entra ID validates token automatically
    var identity = req.HttpContext.User.Identity as ClaimsIdentity;

    // Check roles
    var roles = req.HttpContext.User.FindAll("roles").Select(c => c.Value);
    if (!roles.Contains("api.reader"))
        return new UnauthorizedResult();

    var functionAId = req.HttpContext.User.FindFirst("appid")?.Value;
    log.LogInformation($"Authorized call from {functionAId}");

    return new OkObjectResult(new { message = "Success" });
}
```

## Key Benefits

| Aspect         | Benefit                                            |
| -------------- | -------------------------------------------------- |
| **No Secrets** | Managed identity handles credentials automatically |
| **No Config**  | Role assignment done in portal, not in code        |
| **Secure**     | JWT token from Entra ID, role-based access control |
| **Scalable**   | Works across subscriptions and tenants             |
| **Auditable**  | All calls logged with caller identity              |

## Security Properties

✅ **Authentication**: Token proves Function A's identity  
✅ **Authorization**: Role claim controls access  
✅ **No Token Sharing**: Each function gets its own token  
✅ **Expiration**: Tokens have short TTL (~1 hour)  
✅ **Credential-less**: No secrets stored anywhere
