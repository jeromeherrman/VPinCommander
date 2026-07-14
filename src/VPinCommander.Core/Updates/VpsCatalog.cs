using System.Text.Json;
using System.Text.Json.Serialization;

namespace VPinCommander.Core.Updates;

/// <summary>One game in the Virtual Pinball Spreadsheet database (vpsdb.json).</summary>
public sealed class VpsGame
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("manufacturer")] public string? Manufacturer { get; set; }
    [JsonPropertyName("year")] public int? Year { get; set; }
    [JsonPropertyName("tableFiles")] public List<VpsTableFile>? TableFiles { get; set; }
}

public sealed class VpsTableFile
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("version")] public string? Version { get; set; }
    [JsonPropertyName("tableFormat")] public string? TableFormat { get; set; }
    /// <summary>JavaScript epoch milliseconds.</summary>
    [JsonPropertyName("updatedAt")] public long? UpdatedAt { get; set; }
    [JsonPropertyName("urls")] public List<VpsUrl>? Urls { get; set; }
    [JsonPropertyName("authors")] public List<string>? Authors { get; set; }
}

public sealed class VpsUrl
{
    [JsonPropertyName("url")] public string? Url { get; set; }
    [JsonPropertyName("broken")] public bool? Broken { get; set; }
}

public static class VpsCatalog
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public static IReadOnlyList<VpsGame> Parse(string json) =>
        JsonSerializer.Deserialize<List<VpsGame>>(json, Options) ?? new List<VpsGame>();
}
