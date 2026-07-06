using System.Diagnostics;
using AspireGuide.Data;
using AspireGuide.Data.Entities;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Trace;

namespace AspireGuide.MigrationService;

public class Worker(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime hostApplicationLifetime) : BackgroundService
{
    public const string ActivitySourceName = "Migrations";
    private static readonly ActivitySource s_activitySource = new(ActivitySourceName);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var activity = s_activitySource.StartActivity("Migrating database", ActivityKind.Client);

        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await RunMigrationAsync(dbContext, cancellationToken);
            await SeedDataAsync(dbContext, cancellationToken);
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            throw;
        }

        hostApplicationLifetime.StopApplication();
    }

    private static async Task RunMigrationAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        });
    }

    private static async Task SeedDataAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        if (await dbContext.Todos.AnyAsync(cancellationToken))
            return;

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            await dbContext.Todos.AddRangeAsync(
                [
                    new Todo
                    {
                        Title = "Setup CI/CD pipeline",
                        Description = "Configure GitHub Actions workflow for build and deploy.",
                        IsCompleted = false,
                        DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7))
                    },
                    new Todo
                    {
                        Title = "Write unit tests",
                        Description = "Add unit tests for the data and API layers.",
                        IsCompleted = false,
                        DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14))
                    }
                ],
                cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }
}
