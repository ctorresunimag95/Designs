## Description

### Summary

<!-- Describe what changed and why. Include the affected project(s), service(s), or resource(s). -->

### Related issue

Fixes/Apply # (issue)

### Dependencies and configuration

<!-- List package, SDK, Docker image, emulator, Azure resource, migration, or configuration changes. -->

<!-- Note any new environment variables, user secrets, App Configuration keys, Key Vault secrets, or Aspire parameters. -->


## Screenshots

Screenshots or a screen recording of the visual changes associated with this PR.

(Feel free to delete this section for non-visual changes.)

## Project-specific considerations

- [ ] AppHost topology and resource dependencies are updated where required.
- [ ] Local emulators/containers and persistent-volume behavior are documented if changed.
- [ ] Service connections and configuration follow the local-development and Azure deployment patterns.
- [ ] Authentication, authorization, managed identity, and data-plane permissions were reviewed for changes involving Keycloak, Microsoft Entra ID, Key Vault, Storage, Service Bus, or App Configuration.
- [ ] Database model or migration changes include the required EF Core migration and migration-service considerations.
- [ ] Azure Functions triggers, bindings, host storage, and Service Bus configuration were updated and tested where applicable.
- [ ] Relevant documentation and guides were updated, including setup, deployment, or troubleshooting instructions.

## Validation

<!-- Record the commands, tests, or manual checks used to validate this change. -->

- [ ] `dotnet build` passes for affected projects.
- [ ] Unit and integration tests pass for affected projects.
- [ ] The AppHost starts successfully and affected resources become healthy in the Aspire dashboard.
- [ ] Relevant API, Service Bus, Storage, Function, authentication, migration, or CLI scenarios were verified.
- [ ] No secrets, connection strings, tokens, passwords, or generated local state were committed.

## Checklist

- Is this feature complete?
  - [ ] Yes. Ready to ship.
  - [ ] No. Follow-up changes expected.
- Does this change require a migration, deployment, rollback, or post-deployment step?
  - [ ] No
  - [ ] Yes. Details are included above.
- Are unit, integration, or scenario tests included or updated where relevant?
  - [ ] Yes
  - [ ] Not applicable
  - [ ] No. Reason: <!-- Explain why. -->