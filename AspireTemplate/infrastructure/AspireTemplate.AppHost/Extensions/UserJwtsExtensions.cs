using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace AspireTemplate.AppHost.Extensions;

#pragma warning disable ASPIREINTERACTION001

internal static class UserJwtsExtensions
{
    /// <summary>
    /// Adds a "Generate Dev Token" command to the Aspire dashboard for the API resource.
    /// Clicking the button opens a dialog with pre-filled inputs (name, roles, audience, validity).
    /// The generated token is returned in the dashboard notification, ready to paste into a
    /// Bearer header. As a side-effect, dotnet user-jwts writes the issuer config into the
    /// project's appsettings.Development.json so the API accepts the token automatically.
    /// </summary>
    internal static IResourceBuilder<ProjectResource> WithUserJwtCommands(
        this IResourceBuilder<ProjectResource> builder)
    {
        var projectPath = builder.Resource.GetProjectMetadata().ProjectPath;

        builder.WithCommand(
            "generate-user-jwt",
            "Generate Dev Token",
            async context =>
            {
                var name = context.Arguments.GetString("name") ?? "dev-user";
                var roles = context.Arguments.GetString("roles") ?? "api-reader";
                var audience = context.Arguments.GetString("audience") ?? "sample-api";
                var validFor = context.Arguments.GetString("validFor") ?? "1d";

                // Use --claim roles=<value> instead of --role so the claim name is "roles"
                // (Azure AD convention) rather than "role" (WIF/ClaimTypes.Role convention).
                // This keeps RoleClaimType = "roles" consistent across Keycloak, Azure AD, and user-jwts.
                var roleFlags = string.Join(" ",
                    roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                         .Select(r => $"--claim roles={r}"));

                var psi = new ProcessStartInfo("dotnet",
                    $"user-jwts create --project \"{projectPath}\" --name \"{name}\" {roleFlags} --audience {audience} --valid-for {validFor} --output token")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };

                using var proc = Process.Start(psi)!;
                var token = (await proc.StandardOutput.ReadToEndAsync(context.CancellationToken)).Trim();
                var error = await proc.StandardError.ReadToEndAsync(context.CancellationToken);
                await proc.WaitForExitAsync(context.CancellationToken);

                if (proc.ExitCode == 0 && !string.IsNullOrEmpty(token))
                {
                    var payload = new { Token = token };
                    string jsonResult = System.Text.Json.JsonSerializer.Serialize(payload);

                    return new ExecuteCommandResult
                    {
                        Success = true,
                        Message = "Token Generated",
                        Data = new CommandResultData
                        {
                            Format = CommandResultFormat.Json,
                            Value = jsonResult,
                            DisplayImmediately = true,
                        }
                    };
                }

                context.Logger.LogError("dotnet user-jwts failed with exit code {ExitCode}. Error: {Error}", proc.ExitCode, error);
                return new ExecuteCommandResult { Success = false, Message = $"dotnet user-jwts failed: {error}" };
            },
            new CommandOptions
            {
                IconName = "Key",
                Arguments =
                [
                    new InteractionInput
                    {
                        Name         = "name",
                        Label        = "Name",
                        InputType    = InputType.Text,
                        Required     = true,
                        Value = "dev-user",
                    },
                    new InteractionInput
                    {
                        Name         = "roles",
                        Label        = "Roles (comma-separated)",
                        InputType    = InputType.Text,
                        Required     = true,
                        Value = "api-reader",
                    },
                    new InteractionInput
                    {
                        Name         = "audience",
                        Label        = "Audience",
                        InputType    = InputType.Text,
                        Required     = true,
                        Value = "sample-api",
                    },
                    new InteractionInput
                    {
                        Name         = "validFor",
                        Label        = "Valid for (e.g. 1d, 8h, 365d)",
                        InputType    = InputType.Text,
                        Required     = false,
                        Value = "1d",
                    },
                ]
            });

        return builder;
    }
}

#pragma warning restore ASPIREINTERACTION001
