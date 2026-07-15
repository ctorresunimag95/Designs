# Service Bus Configuration Guide

Use this guide to add queues, topics, subscriptions, message filters, and local test commands to an Aspire AppHost.

## 1. Add the Service Bus resource

```csharp
var serviceBus = builder.AddAzureServiceBus("serviceBus")
    .RunAsEmulator(emulator => emulator
        .WithLifetime(ContainerLifetime.Persistent));
```

Remove `RunAsEmulator` when the AppHost should use an existing Azure connection string supplied through configuration.

## 2. Add a queue

Use a resource name for Aspire references and a separate entity name for the Service Bus queue:

```csharp
serviceBus.AddServiceBusQueue(
    name: "orders-queue",
    queueName: "orders.v1");
```

Configure delivery behavior when needed:

```csharp
serviceBus.AddServiceBusQueue("orders-retry", "orders.retry.v1")
    .WithProperties(properties =>
    {
        properties.MaxDeliveryCount = 5;
        properties.LockDuration = TimeSpan.FromSeconds(30);
        properties.DefaultMessageTimeToLive = TimeSpan.FromMinutes(10);
        properties.DeadLetteringOnMessageExpiration = true;
    });
```

## 3. Add a topic and subscription

```csharp
var topic = serviceBus.AddServiceBusTopic("events", "events.v1");

topic.AddServiceBusSubscription("api-subscription", "api-subscription")
    .WithProperties(subscription =>
    {
        subscription.MaxDeliveryCount = 3;
        subscription.LockDuration = TimeSpan.FromMinutes(1);
        subscription.DeadLetteringOnMessageExpiration = true;
    });
```

Subscriptions receive copies of messages published to the topic. Add separate subscriptions when consumers need independent retry and retention behavior.

## 4. Add a subscription rule

Without rules, all topic messages are delivered. Add a correlation or SQL filter when a subscription should receive selected messages:

```csharp
topic.AddServiceBusSubscription("orders-reader")
    .WithProperties(subscription =>
    {
        subscription.Rules.Add(new AzureServiceBusRule("orders-only")
        {
            CorrelationFilter = new()
            {
                Subject = "order-created"
            }
        });
    });
```

Use message properties consistently with the producer and document the filtering contract.

## 5. Connect an API or Function App

```csharp
var api = builder.AddProject<Projects.AspireTemplate_SampleApi>("sample-api")
    .WithReference(serviceBus, connectionName: "AzureServiceBus")
    .WaitFor(serviceBus);

var function = builder.AddAzureFunctionsProject<Projects.AspireTemplate_SampleFunction>("sample-function")
    .WithEnvironment("SERVICE_BUS", serviceBus)
    .WaitFor(serviceBus);
```

## 6. Test messages locally

```csharp
topic.WithInteractiveServiceBusTestCommand(
    "send-test-message",
    "Send Test Message");
```

Start the AppHost, open the dashboard, select the command, and enter the queue or topic name and message body. Send to the topic rather than a subscription when testing a subscription consumer.

## 7. Prepare for Azure

Replace the emulator with a managed namespace, use Microsoft Entra authentication or managed identity, assign least-privilege data-plane roles, and monitor dead-letter queues. Do not use emulator credentials in a deployed environment.
