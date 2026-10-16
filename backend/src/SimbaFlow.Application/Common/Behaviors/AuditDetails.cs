using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SimbaFlow.Application.Common.Behaviors;

/// <summary>
/// What the change log records about a request: whether it is recorded at all, which record it
/// was about, and what was asked for — with passwords, codes and tokens blanked and files left out.
/// </summary>
public static class AuditDetails
{
    /// <summary>Longest details kept on one row. Longer requests keep their field names only.</summary>
    public const int MaxLength = 8000;

    private const int MaxText = 500;
    private const int MaxItems = 50;
    private const string Hidden = "***";

    // Every page keeps its session alive with this; the sign-in it extends is what is recorded.
    private static readonly HashSet<string> NotRecorded = new(StringComparer.Ordinal)
    {
        "RefreshTokenCommand",
    };

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        MaxDepth = 16,
    };

    /// <summary>
    /// Every request that is not a read goes on the change log, whatever its verb: payments,
    /// stage moves, sign-ins and suspensions as much as creates and deletes. Exports are reads
    /// that take data out of the system, so they are recorded too.
    /// </summary>
    public static bool IsRecorded(string requestName) =>
        !NotRecorded.Contains(requestName)
        && (!requestName.EndsWith("Query", StringComparison.Ordinal)
            || requestName.StartsWith("Export", StringComparison.Ordinal));

    /// <summary>"RecordPaymentCommand" is about a Payment; "LoginCommand" about a Login.</summary>
    public static string EntityType(string requestName)
    {
        var name = requestName;
        foreach (var suffix in new[] { "Command", "Query" })
            if (name.EndsWith(suffix, StringComparison.Ordinal) && name.Length > suffix.Length)
                name = name[..^suffix.Length];

        // Drop the leading verb: the first capital after the first letter starts the noun.
        for (var i = 1; i < name.Length; i++)
            if (char.IsUpper(name[i]))
                return name[i..];

        return name;
    }

    /// <summary>
    /// The record a request was about: the id it created, or else the id it was sent for.
    /// </summary>
    public static string? EntityId(object request, object? response)
    {
        if (response?.GetType().GetProperty("Data")?.GetValue(response) is Guid created && created != Guid.Empty)
            return created.ToString();

        var props = request.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var id = props.FirstOrDefault(p => p.Name == "Id" && p.PropertyType == typeof(Guid))
                 ?? props.FirstOrDefault(p => p.Name.EndsWith("Id", StringComparison.Ordinal) && p.PropertyType == typeof(Guid));

        return id?.GetValue(request) is Guid value && value != Guid.Empty ? value.ToString() : null;
    }

    /// <summary>A request that came back as a failed Result did not happen, though nothing threw.</summary>
    public static bool Succeeded(object? response) =>
        response?.GetType().GetProperty("IsSuccess")?.GetValue(response) is not false;

    /// <summary>
    /// The request as JSON, for the details of its row. Secrets are blanked at any depth, files
    /// and long text are summarised, and anything that cannot be written down is skipped rather
    /// than failing the request.
    /// </summary>
    public static string? Describe(object request)
    {
        try
        {
            var fields = new Dictionary<string, object?>();
            foreach (var prop in request.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.GetIndexParameters().Length > 0 || prop.Name == "EqualityContract") continue;
                // The marker interfaces add these; they describe the gate, not the request.
                if (prop.Name is "RequiredPermission" or "AcceptedPermissions" or "OwnerOnlyMessage"
                    or "AuditEntityType" or "AuditEntityIds") continue;

                fields[prop.Name] = IsSecret(prop.Name) ? Hidden : Summarise(prop.GetValue(request));
            }

            if (fields.Count == 0) return null;

            var node = JsonSerializer.SerializeToNode(fields, Json);
            Scrub(node);
            var json = node?.ToJsonString(Json);

            if (json is not null && json.Length > MaxLength)
                json = JsonSerializer.Serialize(new { truncated = true, fields = fields.Keys.Select(JsonNamingPolicy.CamelCase.ConvertName) }, Json);

            return json;
        }
        catch
        {
            return null;
        }
    }

    public static bool IsSecret(string name)
    {
        var n = name.ToLowerInvariant();
        return n.Contains("password") || n.Contains("secret") || n.Contains("token")
               || n.Contains("otp") || n.Contains("apikey")
               || n is "code" or "recoverycode" or "pin";
    }

    private static object? Summarise(object? value) => value switch
    {
        null => null,
        string s when s.Length > MaxText => $"[{s.Length} characters]",
        string s => s,
        byte[] bytes => $"[file, {bytes.Length} bytes]",
        Stream => "[file]",
        _ when IsFile(value) => FileLabel(value),
        ICollection list when list.Count > MaxItems => $"[{list.Count} items]",
        IEnumerable list and not IDictionary => list.Cast<object?>().Select(Summarise).ToList(),
        _ => value,
    };

    // Upload types live in ASP.NET, which this layer does not reference.
    private static bool IsFile(object value) =>
        value.GetType().GetInterfaces().Any(i => i.Name is "IFormFile" or "IFormFileCollection");

    private static string FileLabel(object file)
    {
        var type = file.GetType();
        var name = type.GetProperty("FileName")?.GetValue(file) as string;
        var length = type.GetProperty("Length")?.GetValue(file);
        return name is null ? "[file]" : $"[file {name}, {length} bytes]";
    }

    private static void Scrub(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(p => p.Key).ToList())
                {
                    if (IsSecret(key)) obj[key] = Hidden;
                    else Scrub(obj[key]);
                }
                break;
            case JsonArray arr:
                foreach (var item in arr) Scrub(item);
                break;
        }
    }
}
