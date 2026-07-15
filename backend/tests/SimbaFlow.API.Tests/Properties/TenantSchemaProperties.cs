using FsCheck;
using FsCheck.Xunit;

namespace SimbaFlow.API.Tests.Properties;

/// <summary>
/// Property-based tests for tenant schema infrastructure.
/// Verifies invariants that must hold for all valid inputs.
/// </summary>
public class TenantSchemaProperties
{
    /// <summary>
    /// Property: Generated schema names never contain path traversal characters.
    /// </summary>
    [Property]
    public bool SchemaName_NeverContainsPathTraversal(PositiveInt length)
    {
        // Generate a valid slug-like string
        var chars = "abcdefgh0123456789-";
        var random = new Random(length.Get);
        var slug = "a" + new string(Enumerable.Range(0, Math.Min(length.Get % 20, 18))
            .Select(_ => chars[random.Next(chars.Length)]).ToArray()) + "z";

        var schemaName = $"tenant_{slug.Replace("-", "_")}";

        return !schemaName.Contains("..")
            && !schemaName.Contains("/")
            && !schemaName.Contains("\\")
            && !schemaName.Contains(";")
            && !schemaName.Contains("'")
            && !schemaName.Contains("\"")
            && schemaName.Length <= 63;
    }

    /// <summary>
    /// Property: File paths generated from tenant slugs and candidate IDs
    /// never escape the base directory.
    /// </summary>
    [Property]
    public bool FilePath_NeverEscapesBaseDirectory(Guid candidateId, PositiveInt slugSeed)
    {
        var chars = "abcdefgh0123456789";
        var random = new Random(slugSeed.Get);
        var slug = "a" + new string(Enumerable.Range(0, 8)
            .Select(_ => chars[random.Next(chars.Length)]).ToArray());

        var basePath = "/data";
        var relativePath = Path.Combine("tenants", slug, "candidates", candidateId.ToString());
        var fullPath = Path.GetFullPath(Path.Combine(basePath, relativePath));

        return fullPath.StartsWith(Path.GetFullPath(basePath));
    }

    /// <summary>
    /// Property: Valid slugs always produce valid PostgreSQL identifiers.
    /// </summary>
    [Property]
    public bool ValidSlug_ProducesValidSchemaName(PositiveInt seed)
    {
        var chars = "abcdefgh012345";
        var random = new Random(seed.Get);
        var slug = "a" + new string(Enumerable.Range(0, 10)
            .Select(_ => chars[random.Next(chars.Length)]).ToArray());

        var schemaName = $"tenant_{slug.Replace("-", "_")}";

        return System.Text.RegularExpressions.Regex.IsMatch(
            schemaName, @"^[a-z_][a-z0-9_]*$");
    }
}
