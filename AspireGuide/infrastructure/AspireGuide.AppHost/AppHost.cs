using AspireGuide.AppHost.Extensions;

var builder = DistributedApplication.CreateBuilder(args);

# region Service Bus

var serviceBus = builder.AddAzureServiceBus("serviceBus")
    .RunAsEmulator(emulator =>
    {
        _ = emulator.WithLifetime(ContainerLifetime.Persistent);
    });

// Add a queue to the service bus with a specific resource name and queue name
serviceBus.AddServiceBusQueue(name: "my-queue", queueName: "my.queue.v1");

// Add a queue to the service bus with a specific resource name and queue name, and configure additional properties
serviceBus.AddServiceBusQueue(name: "my-queue-2", queueName: "my.queue.v2")
    .WithProperties(properties =>
    {
        properties.DeadLetteringOnMessageExpiration = true;
        properties.DefaultMessageTimeToLive = TimeSpan.FromMinutes(10); // PT10M
        properties.DuplicateDetectionHistoryTimeWindow = TimeSpan.FromSeconds(20); // PT20S
        properties.LockDuration = TimeSpan.FromSeconds(30);
        properties.MaxDeliveryCount = 5;
        properties.RequiresDuplicateDetection = false;
        properties.RequiresSession = false;
    });

// Add a topic to the service bus with a specific resource name and topic name
var topic = serviceBus.AddServiceBusTopic(name: "my-topic", topicName: "my.topic.v1")
    .WithProperties(properties =>
    {
        properties.DefaultMessageTimeToLive = TimeSpan.FromMinutes(30); // PT30M
    });
topic.AddServiceBusSubscription("my-topic-subscription")
    .WithProperties(subscription =>
    {
        subscription.DeadLetteringOnMessageExpiration = true;
        subscription.DefaultMessageTimeToLive = TimeSpan.FromMinutes(5); // PT5M
        subscription.LockDuration = TimeSpan.FromMinutes(1); // PT1M
        subscription.MaxDeliveryCount = 3;
        subscription.RequiresSession = false;

        // Subscription rules filter messages sent to the topic, allowing only matching messages to be delivered to the subscription. Multiple rules can be added per subscription, each with its own filter expression.
        // If no rules are defined, all messages sent to the topic will be delivered to the subscription.
        // To add rules to the subscription, use the following pattern:

        /*
        subscription.Rules.Add(new AzureServiceBusRule("app-prop-filter-1")
        {
            CorrelationFilter = new()
            {
                ContentType = "application/text",
                CorrelationId = "id1",
                Subject = "subject1",
                MessageId = "msgid1",
                ReplyTo = "someQueue",
                ReplyToSessionId = "sessionId",
                SessionId = "session1",
                SendTo = "xyz"
            }
        });
        */

    }); // The resource name is also used as the subscription name. To specify a different subscription name, use the overload that accepts a subscriptionName parameter.

topic.WithInteractiveServiceBusTestCommand("send-test-message", "Send Test Message");

# endregion

# region Blob Storage

var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(azurite =>
    {
        _ = azurite.WithLifetime(ContainerLifetime.Persistent)
            // The default data volume for Azurite is /data. If you want to change the data volume, you can do it like this:
            .WithDataVolume("azurite-data")
            .WithBlobPort(10000)
            .WithQueuePort(10001)
            .WithTablePort(10002)
            // The default ports for Azurite are 10000 for Blob, 10001 for Queue, and 10002 for Table. If you want to change the ports, you can do it like this:
            // .WithBlobPort(27000)
            // .WithQueuePort(27001)
            // .WithTablePort(27002)
            ;
    });
var blobs = storage.AddBlobs("blobs")
    .WithDataLoadCommand("seed-sample-data", "Seed Sample Data", containerName: "sample-data", filePath: Path.Combine(AppContext.BaseDirectory, "Content", "SampleFiles"));
/*
    If you want to modify the blob upload options, you can do it like this:
    .WithDataLoadCommand("seed-sample-data", "Seed Sample Data", containerName: "sample-data", filePath: Path.Combine(AppContext.BaseDirectory, "Content", "SampleFiles"), blobOptionsModifier: options =>
    {
        options.AccessTier = AccessTier.Cool;
    });
*/

var queues = storage.AddQueues("queues");
var tables = storage.AddTables("tables");

#endregion

# region Key Vault

// The value can be provided in the AppHost configuration file (appsettings.json), set as a user secret, or through any other standard .NET configuration source.
// appsettings.json example:
// "Parameters": {
//   "KeyVaultUrl": "https://my-key-vault.vault.azure.net/"
// }
var keyVaultUrl = builder.AddParameter("KeyVaultUrl");

#endregion

# region SQL Server

// For local development, you can use the following to configure a SQL Server container.
var sqlPassword = builder.AddParameter("SqlPassword", secret: true);

var sql = builder.AddSqlServer("sql", password: sqlPassword)
    .WithEndpoint(targetPort: 1433, port: 1433)
    .WithDataVolume("sql-data-volume")
    .WithLifetime(ContainerLifetime.Persistent);

var databaseName = "AppDB";
// var creationScript = ScriptHelpers.LoadAllScriptsFromScriptsFolder()
//     .Replace("{{databaseName}}", databaseName);

var db = sql.AddDatabase(databaseName)
    // To initialize the database with a custom script, use WithCreationScript. The script runs once after the SQL Server container starts and the database is created.
    //.WithCreationScript(creationScript)
    // Applies Scripts/PostInit/*.sql on every startup once the database resource is ready.
    // .WithPostInitScripts()
    ;

// The migration service runs EF Core migrations and seeds the database on every AppHost startup.
// It waits for the SQL Server container to be ready, applies pending migrations, then stops.
// Services that depend on the database schema must call WaitForCompletion(migrations) to ensure
// they only start after the database is fully initialized.
var migrations = builder.AddProject<Projects.AspireGuide_MigrationService>("migrations")
    .WithReference(db, "Database")
    .WaitFor(db);

// If you want to connect to a existing SQL Server instance, you can do it like this:
// var sql = builder.AddConnectionString("Sql");
// The value will be read from the AppHost configuration file (appsettings.json), user secrets.
// appsettings.json example:
// "ConnectionStrings": {
//   "Sql": "Server={{serverName}},{{port}};Initial Catalog={{databaseName}};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication="Active Directory Default";"
// }
// If the azure server database is configured to use Active Directory authentication, the connection string should be configured to use "Authentication=Active Directory Default" or "Authentication=Active Directory Interactive" depending on the authentication method you want to use. If you want to use a username and password, you can use "Authentication=Sql Password" and provide the username and password in the connection string.
// In order to make the User Managed Identity authentication work, you should run `az login` in the terminal before running the AppHost, and make sure that the user has the necessary permissions to access the database.

#endregion

# region Identity

// Keycloak runs as a local container that mimics Azure Active Directory using the OpenID Connect protocol.
// It is pre-configured with the aspire-guide realm (Keycloak/realm-export.json) which includes:
//   - sample-api-ui  : public client for interactive user login (Auth Code + PKCE)
//   - sample-api-m2m : confidential client for service-to-service calls (Client Credentials)
//   - dev-user / dev-admin test users with api-reader and api-writer realm roles
//
// The Authority URL is injected into services via Authentication__Authority.
// To swap to real Azure AD in production, set these two environment variables — no code changes needed:
//   Authentication__Authority = https://login.microsoftonline.com/{tenantId}/v2.0
//   Authentication__Audience  = api://{clientId}

var keycloak = builder.AddLocalKeycloak();

#endregion

# region App Configuration

var appConfiguration = builder.AddAzureAppConfiguration("appConfiguration")
    .RunAsEmulator(emulator =>
    {
        emulator
            .WithEnvironment("AZURE_APP_CONFIGURATION_EMULATOR_AUTHENTICATION_ENABLED", "false")
            .WithHostPort(28000)
            .WithLifetime(ContainerLifetime.Persistent)
            .WithDataVolume("appconfig-data");
    });

#endregion

# region Sample Api

var api = builder.AddProject<Projects.AspireGuide_SampleApi>("sample-api")
    .WithReference(serviceBus, connectionName: "AzureServiceBus")
    .WaitFor(serviceBus)
    .WithReference(blobs, connectionName: "AzureBlobStorage")
    .WithReference(db, "Database")
    .WaitForCompletion(migrations)
    .WaitFor(keycloak)
    .WithKeycloakAuthentication(keycloak)
    .WithReference(appConfiguration)    .WaitFor(appConfiguration);

if (keyVaultUrl is not null && !string.IsNullOrWhiteSpace(keyVaultUrl.Resource.ValueExpression))
{
    api.WithEnvironment("KeyVault__Url", keyVaultUrl);
}

#endregion

# region Sample Function

topic.AddServiceBusSubscription("sample-function-subscription");

var function = builder.AddAzureFunctionsProject<Projects.AspireGuide_SampleFunction>("sample-function")
    .WithHostStorage(storage)
    .WaitFor(serviceBus)
    .WithEnvironment("SERVICE_BUS", serviceBus)
    .WithHttpEndpoint(port: 7184, targetPort: 7071, name: "http", isProxied: true);

#endregion

builder.Build().Run();
