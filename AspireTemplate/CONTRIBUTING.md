# Contributing to AspireTemplate

Thank you for contributing to AspireTemplate. This project is a reference application for integrating Azure services with .NET Aspire, so contributions should preserve clarity, reproducibility, and useful documentation for developers who use the repository as a template.

## Before you start

Review the project overview and architecture documentation:

- [README.md](README.md)
- [agents.md](agents.md)
- Resource-specific guides in [docs](docs/)

For changes that affect the contribution workflow, also follow the pull request guidance in [.github/pull_request_template.md](.github/pull_request_template.md).

## Prerequisites

Install the tools required for the area you are changing:

- .NET 10 SDK
- Docker Desktop
- Azure CLI for Azure-authenticated scenarios
- Azure Functions Core Tools when running the Functions project independently
- Git

Some scenarios also require access to Azure resources or local credentials. Never commit credentials, connection strings, tokens, passwords, or generated local state.

## Development setup

1. Fork or clone the repository.
2. Create a feature branch from the default branch.
3. Restore and build the solution:

   ```bash
   dotnet restore AspireTemplate.slnx
   dotnet build AspireTemplate.slnx
   ```

4. Start the local Aspire environment:

   ```bash
   dotnet run --project infrastructure/AspireTemplate.AppHost
   ```

5. Open the Aspire dashboard URL printed by the AppHost.
6. Confirm that affected resources are healthy before testing application behavior.

The AppHost starts local emulators and containers for services such as SQL Server, Service Bus, Storage, Keycloak, and App Configuration. Persistent volumes retain local state. Remove a volume only when intentionally resetting emulator data.

## Making changes

Keep changes focused and follow the existing project structure:

- Update the AppHost when resource topology, dependencies, startup ordering, or connection configuration changes.
- Update the relevant application, Function, migration, or shared Service Defaults project when behavior changes.
- Use managed-identity-compatible patterns for Azure integrations where practical.
- Keep local emulator configuration separate from production configuration.
- Add or update documentation for new resources, configuration keys, setup steps, deployment considerations, or troubleshooting information.
- Do not commit secrets or machine-specific settings.

## Adding new resources

When adding a new resource, update the AppHost configuration and include:
  - Add dedicated documentation in [docs](docs/) for setup, configuration, and troubleshooting.
  - Add the dedicated region in the AppHost configuration with the appropriate resource type, connection details, and startup ordering.
  - Any nuget or external dependency required for the resource must be verified as trusted and compatible with the governance and licensing requirements of the project.

### Database changes

For changes to EF Core entities or mappings:

- Add the appropriate migration.
- Verify that the migration service can apply it during AppHost startup.
- Document any data migration, rollback, or deployment ordering requirements.

### Messaging and Functions changes

For Service Bus or Azure Functions changes, verify the affected queues, topics, subscriptions, filters, triggers, bindings, host storage, retry behavior, and local configuration. Include scenario coverage when behavior cannot be validated by unit tests alone.

### ResourceCli changes

For changes to AspireTemplate.ResourceCli:

- Update the resource catalog, templates, generators, validation, or user experience as appropriate.
- Add or update unit tests in `tests/AspireTemplate.ResourceCli.Tests`.
- Update the CLI README when commands, options, prerequisites, or generated output change.

## Testing and validation

Run the checks relevant to your change. At minimum, affected projects should build successfully:

```bash
dotnet build AspireTemplate.slnx
dotnet test AspireTemplate.slnx
```

When applicable, also verify:

- The AppHost starts successfully.
- Affected resources become healthy in the Aspire dashboard.
- API endpoints and authentication flows work as expected.
- Service Bus messages are sent and processed correctly.
- Blob, Queue, or Table Storage operations work against the local emulator.
- App Configuration refresh and feature flags behave correctly.
- Key Vault configuration remains optional for local development.
- EF Core migrations execute successfully.
- Azure Functions triggers execute with the expected configuration.
- ResourceCli commands produce valid output and generated code.

Record the validation commands and manual checks in the pull request.

## Commit messages

Use [Conventional Commits](https://www.conventionalcommits.org/) where practical:

- `feat: add storage resource catalog entry`
- `fix: handle service bus processor retry`
- `docs: clarify local key vault setup`
- `test: cover resource validation failure`
- `refactor: simplify apphost configuration`

Keep commits focused and avoid including unrelated formatting or generated files.

## Pull requests

Before opening a pull request:

1. Rebase or update the branch with the latest default branch when appropriate.
2. Confirm that the solution and relevant tests pass.
3. Review the changes for secrets, generated state, and environment-specific values.
4. Update documentation and tests where required.
5. Complete the pull request template.
6. Describe any migration, deployment, rollback, or post-deployment steps.

Pull requests should explain the motivation, affected services or resources, validation performed, and any known limitations or follow-up work.

## Reporting security issues

Do not report security vulnerabilities in a public issue. Follow the private reporting process described in `SECURITY.md` when that file is available, or contact the repository maintainers privately.

## Questions

For configuration and resource-specific questions, start with the relevant guide in [docs](docs/) and the project overview in [README.md](README.md).
