using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using AspireTemplate.ResourceCli.Discovery;
using AspireTemplate.ResourceCli.Editing;
using AspireTemplate.ResourceCli.Ux;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AspireTemplate.ResourceCli.Commands;

internal sealed class InitCommand : Command<InitCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--directory <PATH>")]
        [Description("Directory in which to create the AppHost.")]
        public string? Directory { get; init; }

        [CommandOption("--solution <PATH>")]
        [Description("Solution file (.sln/.slnx) to add the AppHost to.")]
        public string? Solution { get; init; }

        [CommandOption("--yes|-y")]
        [Description("Skip confirmation prompts.")]
        public bool Yes { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings) =>
        Run(settings, new SpectreConsoleUx());

    internal static int Run(Settings settings, IConsoleUx ux)
    {
        // ── 1. Determine target directory ─────────────────────────────────────
        var targetDir = settings.Directory;
        if (string.IsNullOrWhiteSpace(targetDir))
        {
            targetDir = AnsiConsole.Ask<string>("Target directory for the new AppHost:", "AppHost");
        }
        targetDir = Path.GetFullPath(targetDir);

        if (Directory.Exists(targetDir) && Directory.GetFiles(targetDir).Length > 0)
        {
            ux.ShowError($"Target directory '{targetDir}' already exists and is not empty.");
            return ExitCodes.Error;
        }

        // ── 2. Locate solution to add the new project to ──────────────────────
        string? solutionPath = settings.Solution;
        if (string.IsNullOrWhiteSpace(solutionPath))
        {
            var repoRoot = RepositoryLocator.FindRoot(System.Environment.CurrentDirectory);
            var solutions = SolutionLocator.Find(repoRoot);
            solutionPath = solutions.Count switch
            {
                0 => null,
                1 => solutions[0],
                _ => AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Which solution should the new AppHost be added to?")
                        .AddChoices(solutions.Append("<none>")))
                    is "<none>" ? null : AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                            .Title("Select solution:")
                            .AddChoices(solutions))
            };
        }

        // ── 3. Scaffold via dotnet new ────────────────────────────────────────
        ux.WriteInfo($"Scaffolding AppHost in '{targetDir}'…");
        Directory.CreateDirectory(targetDir);

        var (exitCode, output, error) = RunDotnet($"new aspire-apphost -o \"{targetDir}\" --force");
        if (exitCode != 0)
        {
            ux.ShowError($"'dotnet new aspire-apphost' failed (exit {exitCode}).\n{error}");
            return ExitCodes.ApplyFailed;
        }

        // ── 4. Verify Aspire SDK marker ───────────────────────────────────────
        var csprojFiles = System.IO.Directory.GetFiles(targetDir, "*.csproj", SearchOption.TopDirectoryOnly);
        if (csprojFiles.Length == 0)
        {
            ux.ShowError($"No .csproj was created in '{targetDir}'. Template output:\n{output}");
            return ExitCodes.ApplyFailed;
        }

        var csprojPath = csprojFiles[0];
        if (!AppHostLocator.IsAppHostProject(csprojPath))
        {
            ux.ShowError($"Generated project '{csprojPath}' does not contain an Aspire AppHost SDK marker. Falling back is not yet implemented.");
            return ExitCodes.ApplyFailed;
        }

        // ── 5. Add to solution ────────────────────────────────────────────────
        if (!string.IsNullOrEmpty(solutionPath) && File.Exists(solutionPath))
        {
            try
            {
                var solutionContent = File.ReadAllText(solutionPath);
                var relativePath = Path.GetRelativePath(Path.GetDirectoryName(solutionPath)!, csprojPath);
                var result = new SolutionEditor().AddProjectReference(solutionContent, relativePath,
                    Path.GetFileNameWithoutExtension(csprojPath), solutionPath);
                if (result.Changes.HasChanges)
                {
                    File.WriteAllText(solutionPath, result.Text);
                    ux.WriteInfo($"Added project to solution: {Path.GetFileName(solutionPath)}");
                }
            }
            catch (Exception ex)
            {
                ux.WriteWarning($"Could not add project to solution: {ex.Message}");
            }
        }

        // ── 6. Write aspire.config.json ───────────────────────────────────────
        var configPath = Path.Combine(targetDir, "aspire.config.json");
        var relCsproj = "./" + Path.GetFileName(csprojPath);
        var config = JsonSerializer.Serialize(new { appHost = new { path = relCsproj } },
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(configPath, config);
        ux.WriteInfo("Wrote aspire.config.json");

        ux.ShowSuccess($"AppHost created at: {targetDir}");

        // ── 7. Continue to add flow ───────────────────────────────────────────
        ux.WriteInfo("Continuing to 'add' — select resources to install.");
        return AddCommand.Run(
            new AddCommand.Settings { Root = targetDir, Yes = settings.Yes },
            ux);
    }

    private static (int ExitCode, string Output, string Error) RunDotnet(string arguments)
    {
        var psi = new ProcessStartInfo("dotnet", arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        using var process = Process.Start(psi)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output, error);
    }
}
