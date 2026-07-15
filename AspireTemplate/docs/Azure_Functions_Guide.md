# Azure Functions Configuration Guide

Use this guide to add an isolated-worker Function App to an Aspire AppHost and connect it to Service Bus.

## 1. Register the Function App

```csharp
var function = builder.AddAzureFunctionsProject<Projects.AspireTemplate_SampleFunction>("sample-function")
    .WithHostStorage(storage)
    .WithEnvironment("SERVICE_BUS", serviceBus)
    .WaitFor(serviceBus)
    .WithHttpEndpoint(
        port: 7184,
        targetPort: 7071,
        name: "http",
        isProxied: true);
```

The `SERVICE_BUS` name must match the `Connection` value used by the Service Bus trigger.

## 2. Add an HTTP trigger

Use the isolated-worker attributes in the Function App project:

```csharp
[Function(nameof(SampleHttpTrigger))]
public IActionResult Run(
    [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
{
    return new OkObjectResult("Welcome to Azure Functions!");
}
```

The Aspire endpoint is proxied to host port `7184`. When running the Functions host directly, use the local Functions URL and its function key when required.

## 3. Add a Service Bus trigger

```csharp
[Function(nameof(ServiceBusTrigger))]
public void Run(
    [ServiceBusTrigger(
        topicName: "my.topic.v1",
        subscriptionName: "sample-function-subscription",
        Connection = "SERVICE_BUS")] ServiceBusReceivedMessage message,
    ServiceBusMessageActions messageActions)
{
    // Process the message.
}
```

Create the matching topic and subscription in the AppHost, then use the Service Bus dashboard command to send a test message to the topic.

## 4. Configure settings

Keep local settings in `local.settings.json` and deployed settings in Function App configuration. Do not commit real secrets. The Function App also receives host storage through `.WithHostStorage(storage)`.

## 5. Prepare for Azure

Deploy the Function App with managed identity where possible, configure its storage and Service Bus settings through the platform, and monitor trigger failures and dead-letter messages.
