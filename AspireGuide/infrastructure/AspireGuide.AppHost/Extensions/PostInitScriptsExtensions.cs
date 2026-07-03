using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AspireGuide.AppHost.Extensions;

public static class PostInitScriptsExtensions
{
    public static IResourceBuilder<SqlServerDatabaseResource> WithPostInitScripts(
        this IResourceBuilder<SqlServerDatabaseResource> builder)
    {
        builder.OnResourceReady(async (resource, evt, ct) =>
        {
            var logger = evt.Services.GetRequiredService<ILogger<SqlServerDatabaseResource>>();

            var script = ScriptHelpers.LoadAllScriptsFromPostInitFolder();
            if (string.IsNullOrWhiteSpace(script))
            {
                logger.LogInformation("No PostInit scripts found; skipping {Resource}.", resource.Name);
                return;
            }

            var connectionString = await resource.ConnectionStringExpression.GetValueAsync(ct);

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);

            foreach (var batch in SplitSqlBatches(script))
            {
                await using var cmd = new SqlCommand(batch, connection);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            logger.LogInformation("Applied PostInit scripts to {Resource}.", resource.Name);
        });

        return builder;
    }

    private static IEnumerable<string> SplitSqlBatches(string script) =>
        script
            .Split(["\nGO", "\r\nGO"], StringSplitOptions.RemoveEmptyEntries)
            .Select(b => b.Trim())
            .Where(b => !string.IsNullOrWhiteSpace(b));
}
