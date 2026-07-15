using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.Azure;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace AspireTemplate.AppHost.Extensions;

#pragma warning disable ASPIREINTERACTION001

/// <summary>
/// Extension methods for the Azure Service Bus resource.
/// </summary>
[SuppressMessage("Reliability", "CA2012:Use ValueTasks correctly")]
internal static class ServiceBusExtensions
{
    /// <summary>
    /// Add an interactive test command button to the Aspire UI.
    /// </summary>
    /// <param name="builder">The Azure service bus resource.</param>
    /// <param name="commandName">The unique command name.</param>
    /// <param name="displayName">The display name shown in Aspire.</param>
    /// <returns>A builder for the Azure service bus resource.</returns>
    internal static IResourceBuilder<T> WithInteractiveServiceBusTestCommand<T>(
        this IResourceBuilder<T> builder,
        string commandName,
        string displayName) where T : IResourceWithParent<AzureServiceBusResource>
    {
        builder.EnsureServiceBusClientRegistered();

        builder.WithCommand(commandName, displayName, async context =>
        {
            var logger = context.ServiceProvider.GetRequiredService<ILogger<Program>>();

            var queueOrTopicName = context.Arguments.GetString("queueOrTopicName") ?? string.Empty;
            var messageBody = context.Arguments.GetString("message") ?? string.Empty;

            if (string.IsNullOrWhiteSpace(queueOrTopicName) || string.IsNullOrWhiteSpace(messageBody))
            {
                return new ExecuteCommandResult
                {
                    Success = false,
                    Message = "Queue or topic name and message body are required."
                };
            }

            var sbClient = context.ServiceProvider.GetRequiredService<ServiceBusClient>();

            await sbClient.CreateSender(queueOrTopicName)
                .SendMessageAsync(new ServiceBusMessage(messageBody), context.CancellationToken);

            logger.LogInformation("Message sent to '{QueueOrTopicName}': {MessageBody}", queueOrTopicName, messageBody);

            return new ExecuteCommandResult
            {
                Success = true,
                Message = $"Message sent to '{queueOrTopicName}'."
            };
        }, new CommandOptions
        {
            IconName = "Send",
            Arguments =
            [
                new InteractionInput
                {
                  Name = "queueOrTopicName",
                  Label = "Queue or topic name",
                    InputType = InputType.Text,
                    Required = true,
                },
                new InteractionInput
                {
                    Name = "message",
                    Label = "Message body",
                    InputType = InputType.Text,
                    Required = true,
                }
            ]
        });

        return builder;
    }

    private static void EnsureServiceBusClientRegistered<T>(this IResourceBuilder<T> builder) where T : IResourceWithParent<AzureServiceBusResource>
    {
        builder.ApplicationBuilder.Services.TryAddSingleton(provider =>
        {
            var connectionString = builder.Resource.Parent.ConnectionStringExpression
                .GetValueAsync(CancellationToken.None).GetAwaiter().GetResult();

            return new ServiceBusClient(connectionString);
        });
    }
}
#pragma warning restore ASPIREINTERACTION001