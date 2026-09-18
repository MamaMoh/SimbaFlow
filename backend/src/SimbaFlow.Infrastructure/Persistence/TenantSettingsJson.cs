using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SimbaFlow.Domain.Entities.Tenancy;

namespace SimbaFlow.Infrastructure.Persistence;

/// <summary>
/// TenantSettings lives as a JSON string on TenantInfo. EF only notices a change when the
/// converted value (or a comparer) says it changed — mutating nested intake fields on the same
/// object is otherwise a silent no-op, which is how a settings save can toast success and still
/// reload as empty.
/// </summary>
public static class TenantSettingsJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static readonly ValueComparer<TenantSettings> Comparer = new(
        (left, right) => Serialize(left) == Serialize(right),
        value => Serialize(value).GetHashCode(),
        value => Deserialize(Serialize(value)));

    public static string Serialize(TenantSettings? value) =>
        JsonSerializer.Serialize(value ?? new TenantSettings(), Options);

    public static TenantSettings Deserialize(string? json) =>
        string.IsNullOrEmpty(json)
            ? new TenantSettings()
            : JsonSerializer.Deserialize<TenantSettings>(json, Options) ?? new TenantSettings();
}
