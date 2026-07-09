# AspireGuide

AspireGuide is a starter-friendly reference for configuring Azure services in .NET Aspire applications. It includes practical snippets, working samples, and guidance to help you set up common Azure integrations quickly.

## What's Included

This guide covers configuration patterns for:

- **Service Bus** — messaging infrastructure setup and integration
- **Azure Storage** — blob, queue, and table storage configuration
- **Key Vault** — secure credential and secrets management
- **SQL Server** — database resource setup and migrations

## Documentation

For detailed guidance, refer to the following resources:

- [SQL Configuration Guide](docs/SQL_Configuration_Guide.md) — Step-by-step SQL Server setup and configuration
- [Azure Login & Dev Container Guide](docs/Azure_Login_DevContainer_Guides.md) — Authentication and local development environment setup
- [Keycloak Token Generation Guide](docs/Keycloak_Token_Generation_Guide.md) — Keycloak integration and token generation for secure access

## Getting Started

### Sample AppHost Configuration

The [infrastructure/AspireGuide.AppHost/AppHost.cs](infrastructure/AspireGuide.AppHost/AppHost.cs) file contains a complete sample showing how to configure all supported Azure services. It demonstrates:

- Service Bus queues, topics, and subscriptions with property configurations
- Azure Storage (Blobs, Queues, Tables) with Azurite emulator setup
- Key Vault integration
- SQL Server with database initialization and EF Core migrations

You can use this file as a reference for your own projects or copy parts of it into your Aspire AppHost as a starting point.

### Browse Examples

Browse the code examples and documentation to find configuration patterns matching your use case. Each service area includes working code snippets that can be adapted to your project.
