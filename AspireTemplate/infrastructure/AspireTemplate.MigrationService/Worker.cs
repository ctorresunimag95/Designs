using System.Diagnostics;
using AspireTemplate.Data;
using Microsoft.EntityFrameworkCore;

namespace AspireTemplate.MigrationService;

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
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await ExecuteSeedScriptsAsync(dbContext, cancellationToken);
        });

        // Alternative: Uncomment the following code to seed sample data using the AppDbContext directly.
        // if (await dbContext.Todos.AnyAsync(cancellationToken))
        //     return;

        // var strategy = dbContext.Database.CreateExecutionStrategy();
        // await strategy.ExecuteAsync(async () =>
        // {
        //     await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        //     await dbContext.Todos.AddRangeAsync(
        //         [
        //             new Todo
        //             {
        //                 Title = "Setup CI/CD pipeline",
        //                 Description = "Configure GitHub Actions workflow for build and deploy.",
        //                 IsCompleted = false,
        //                 DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7))
        //             },
        //             new Todo
        //             {
        //                 Title = "Write unit tests",
        //                 Description = "Add unit tests for the data and API layers.",
        //                 IsCompleted = false,
        //                 DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14))
        //             }
        //         ],
        //         cancellationToken);

        //     await dbContext.SaveChangesAsync(cancellationToken);
        //     await transaction.CommitAsync(cancellationToken);
        // });
    }

    private static async Task ExecuteSeedScriptsAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var assembly = typeof(AppDbContext).Assembly;
        var scripts = assembly.GetManifestResourceNames()
                              .Where(n => n.StartsWith("AspireTemplate.Data.SeedData.") && n.EndsWith(".sql"))
                              .Order();

        foreach (var resourceName in scripts)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream);
            var sql = await reader.ReadToEndAsync(cancellationToken);
            await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
    }
}
