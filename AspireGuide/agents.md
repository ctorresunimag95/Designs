# AspireGuide Architecture & Services

This document describes the distributed services and agents that make up the AspireGuide application ecosystem.

## Overview

AspireGuide is composed of multiple independent services coordinated through a .NET Aspire AppHost, with Azure Services providing the underlying infrastructure for messaging, storage, and configuration.

## Core Services

### 1. AppHost (`infrastructure/AspireGuide.AppHost`)

**Purpose:** The orchestration layer that defines the complete application topology for local development and deployment.

**Responsibilities:**

- Provisions local emulators for Azure services (Service Bus, Storage, SQL Server, etc.)
- Configures resource connections and environment variables
- Coordinates startup dependencies
- Manages persistent volumes for stateful services

**Key Components:**

- Service Bus configuration (queues, topics, subscriptions)
- Azure Storage setup (Blob, Queue, Table)
- SQL Server and EF Core migrations
- Keycloak authentication service
- App Configuration and Key Vault integration

**Configuration Extensions:**

- `BlobExtensions.cs` — Blob Storage setup and seeding
- `ServiceBusExtensions.cs` — Service Bus queue/topic management
- `KeycloakExtensions.cs` — OpenID Connect authentication
- `PostInitScriptsExtensions.cs` — Post-deployment initialization
- `ScriptHelpers.cs` — Utility helpers for resource setup

---

### 2. SampleApi (`src/AspireGuide.SampleApi`)

**Purpose:** REST API demonstrating resource integration patterns for local and Azure-deployed scenarios.

**Responsibilities:**

- Exposes HTTP endpoints for business operations
- Processes Azure Service Bus messages asynchronously
- Accesses Azure Storage (Blob, Queue, Table)
- Loads and manages configuration from App Configuration service
- Validates JWT tokens from Keycloak

**Key Subsystems:**

#### Service Bus Integration

- `ServiceBus/IBaseProcessor.cs` — Interface for message processing
- `ServiceBus/BaseProcessor.cs` — Base implementation with error handling and retries
- `ServiceBus/SampleMessageProcessor.cs` — Concrete processor for sample messages
- `ServiceBus/BusExtensions.cs` — Service Bus registration and configuration

#### Blob Storage Integration

- `BlobStorage/IBlobStorageService.cs` — Interface for blob operations
- `BlobStorage/BlobStorageService.cs` — Implementation with upload, download, delete
- `BlobStorage/BlobStorageServiceCollection.cs` — Dependency injection setup

#### Key Vault & Secrets

- `KeyVault/KeyVaultConfigurationExtensions.cs` — Loads secrets using DefaultAzureCredential

#### Configuration Management

- `AppConfiguration/RemoveAuthorizationHeaderPolicy.cs` — Custom policy for App Configuration access

---

### 3. Data Layer (`src/AspireGuide.Data`)

**Purpose:** Entity Framework Core models, migrations, and database context for the application.

**Responsibilities:**

- Defines data models and relationships
- Manages database migrations
- Provides DbContext for data access

**Entities:**

- `Entities/Todo.cs` — Todo list item
- `Entities/ToDoTask.cs` — Subtask or detailed task

**Configuration:**

- `Configurations/TodoConfiguration.cs` — Todo entity mapping
- `Configurations/ToDoTaskConfiguration.cs` — ToDoTask entity mapping

**Factory & Migrations:**

- `AppDbContext.cs` — Main database context
- `AppDbContextFactory.cs` — Design-time factory for EF tooling
- `Migrations/` — Version-controlled schema changes

---

### 4. Azure Functions (`src/AspireGuide.SampleFunction`)

**Purpose:** Serverless compute for event-driven workloads triggered by HTTP requests or Service Bus messages.

**Responsibilities:**

- Handle HTTP requests via HttpTrigger
- Process Service Bus messages asynchronously
- Execute isolated worker functions with secure dependency injection

**Triggers:**

- `SampleHttpTrigger.cs` — HTTP-triggered function
- `ServiceBusTrigger.cs` — Service Bus queue/topic triggered function

**Configuration:**

- `Program.cs` — Function app startup and dependency registration

---

### 5. Migration Service (`infrastructure/AspireGuide.MigrationService`)

**Purpose:** Automated EF Core database schema migrations for AppHost startup.

**Responsibilities:**

- Runs migrations on application startup
- Ensures database schema is current
- Seeds initial data if configured

**Components:**

- `Program.cs` — Service entry point
- `Worker.cs` — Migration execution logic

---

### 6. Service Defaults (`infrastructure/AspireGuide.ServiceDefaults`)

**Purpose:** Shared cross-cutting concerns and configuration for all services.

**Responsibilities:**

- Health check setup and registration
- Resilience policies (retries, circuit breakers)
- Authentication and authorization extensions
- Logging and observability configuration

**Key Components:**

- `Extensions.cs` — Service registration and middleware
- `AuthenticationExtensions.cs` — JWT validation and identity setup
- `KeycloakClaimsTransformation.cs` — Token claims normalization and transformation

---

## External Services & Infrastructure

### Azure Service Bus

- **Queues:** `my.queue.v1`, `my.queue.v2` — Point-to-point messaging
- **Topics & Subscriptions:** `my.topic.v1` with filtering rules — Pub/Sub messaging
- **Local Emulator:** Runs as a persistent Docker container during development

### Azure Storage (Azurite)

- **Blob Storage:** `sample-data` container for files
- **Queue Storage:** Event-driven queue operations
- **Table Storage:** NoSQL table storage
- **Local Emulator:** Persistent volume for development data

### SQL Server

- **Database:** AppDbContext with Todo entities
- **Migrations:** Managed by EF Core and MigrationService
- **Local Emulator:** Docker container with persistent volume

### App Configuration

- **Key-Values:** Feature flags and settings
- **Refresh:** Dynamic configuration updates
- **Labels:** Environment-specific configuration
- **Local Emulator:** Persistent container for development

### Keycloak (OpenID Connect)

- **Authentication:** Issues JWT tokens for users
- **Machine-to-Machine:** M2M client credentials flow
- **Local Realm:** Development realm with test users and API clients
- **Local Emulator:** Docker container

### Azure Key Vault

- **Secrets:** Sensitive configuration loaded via DefaultAzureCredential
- **Optional:** Disabled when `KeyVaultUrl` is empty
- **Production:** Replaces local development credentials

---

## Development Workflow

1. **AppHost Startup:** `dotnet run --project infrastructure/AspireGuide.AppHost`
2. **Aspire Dashboard:** Monitor resources, view endpoints, run custom commands
3. **API Calls:** Hit SampleApi endpoints via REST
4. **Message Processing:** Publish to Service Bus, Functions and SampleApi process
5. **Database Access:** EF Core queries against SQL Server
6. **Authentication:** Obtain JWT from Keycloak, include in protected endpoint calls

---

## Deployment Notes

For production deployment:

- Replace emulators with managed Azure services
- Use managed identities instead of connection strings
- Load Key Vault secrets via DefaultAzureCredential
- Configure App Configuration with production key-values
- Replace Keycloak with Microsoft Entra ID
- Scale services independently based on load

---

## Configuration & Secrets

- **Local Development:** User Secrets, environment variables, emulator defaults
- **Azure Deployment:** Managed Identities, Azure Key Vault, App Configuration
- **Never commit:** Connection strings, client secrets, personal access tokens, passwords
