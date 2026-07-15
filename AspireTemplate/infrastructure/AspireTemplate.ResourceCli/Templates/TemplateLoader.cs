using System.Reflection;
using System.Text.Json;

namespace AspireTemplate.ResourceCli.Templates;

/// <summary>
/// Loads embedded template resources (region snippets and companion files).
/// </summary>
internal static class TemplateLoader
{
    private static readonly Assembly Assembly = typeof(TemplateLoader).Assembly;

    private static string ResourcePrefix => "AspireTemplate.ResourceCli.Templates.";

    /// <summary>
    /// Loads a region template for the given resource key.
    /// Returns the full content including the stable AspireTemplate markers.
    /// </summary>
    public static string LoadRegion(string resourceKey)
    {
        var name = $"{ResourcePrefix}regions.{resourceKey}.txt";
        return ReadString(name);
    }

    /// <summary>
    /// Opens a stream for a companion file. Caller is responsible for disposing.
    /// <paramref name="relativePath"/> uses forward slashes, e.g. "Extensions/BlobExtensions.cs".
    /// </summary>
    public static Stream OpenCompanionFile(string relativePath)
    {
        var name = $"{ResourcePrefix}files.{relativePath.Replace('/', '.').Replace('\\', '.')}";
        return OpenStream(name);
    }

    /// <summary>
    /// Reads a companion file as a UTF-8 string.
    /// </summary>
    public static string ReadCompanionFile(string relativePath)
    {
        using var stream = OpenCompanionFile(relativePath);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Validates that the companion file for <paramref name="relativePath"/> is valid JSON.
    /// </summary>
    public static bool IsValidJson(string relativePath)
    {
        try
        {
            var text = ReadCompanionFile(relativePath);
            JsonDocument.Parse(text);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Returns all embedded resource names, useful for diagnostics.
    /// </summary>
    public static IReadOnlyList<string> GetAllResourceNames() =>
        Assembly.GetManifestResourceNames();

    private static string ReadString(string resourceName)
    {
        using var stream = OpenStream(resourceName);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static Stream OpenStream(string resourceName)
    {
        var stream = Assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            var available = string.Join(", ", Assembly.GetManifestResourceNames());
            throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' not found. Available: {available}");
        }
        return stream;
    }
}
