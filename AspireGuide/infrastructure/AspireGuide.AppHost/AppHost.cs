using AspireGuide.AppHost.Extensions;

var builder = DistributedApplication.CreateBuilder(args);

# region Service Bus

var serviceBus = builder.AddAzureServiceBus("serviceBus")
    .RunAsEmulator(emulator =>
    {
        _ = emulator.WithLifetime(ContainerLifetime.Persistent);
    });

// Add a queue to the service bus with a specific resouce name and queue name
serviceBus.AddServiceBusQueue(name: "my-queue", queueName: "my.queue.v1");

// Add a queue to the service bus with a specific resouce name and queue name, and configure additional properties
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

// Add a topic to the service bus with a specific resouce name and topic name
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

        // Subscription rules allow you to filter messages that are sent to the topic and only allow certain messages to be delivered to the subscription. You can add multiple rules to a subscription, and each rule can have a different filter expression.
        // If no rules are added, all messages sent to the topic will be delivered to the subscription.
        // If needed to add rules to the subscription, you can do it like this:

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

    }); // Resource name will be used as subscription name aswell. If want to have a different subscription name, use the overload with subscriptionName parameter

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

builder.AddProject<Projects.AspireGuide_SampleApi>("sample-api")
    .WithReference(serviceBus, connectionName: "AzureServiceBus")
    .WaitFor(serviceBus)
    .WithReference(blobs, connectionName: "AzureBlobStorage");

builder.Build().Run();
