using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using GatherBuddy.Classes;

namespace GatherBuddy.CustomInfo;

internal sealed class FishStrategyEntry
{
    public string?  Author     { get; init; }
    public string[] Paragraphs { get; init; } = [];
}

internal sealed class FishStrategyCatalogFile
{
    public string SourceTitle  { get; init; } = string.Empty;
    public string SourceUrl    { get; init; } = string.Empty;
    public string SourceCredit { get; init; } = string.Empty;
    public Dictionary<string, FishStrategyEntry> Strategies { get; init; } = [];
}

internal static class FishStrategyCatalog
{
    private const string ResourceName = "GatherBuddy.CustomInfo.fish_strategies.zh-CN.json";
    private static readonly Lazy<FishStrategyCatalogFile> Catalog = new(Load);

    public static string SourceTitle
        => Catalog.Value.SourceTitle;

    public static string SourceUrl
        => Catalog.Value.SourceUrl;

    public static string SourceCredit
        => Catalog.Value.SourceCredit;

    public static bool TryGet(Fish fish, out FishStrategyEntry strategy)
        => Catalog.Value.Strategies.TryGetValue(fish.Name.ChineseSimplified, out strategy!);

    private static FishStrategyCatalogFile Load()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
              ?? throw new InvalidDataException($"Embedded strategy resource {ResourceName} was not found.");
            return JsonSerializer.Deserialize<FishStrategyCatalogFile>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            }) ?? throw new InvalidDataException("The embedded fish strategy catalog is empty.");
        }
        catch (Exception ex)
        {
            GatherBuddy.Log.Warning($"加载钓鱼攻略失败: {ex.Message}");
            return new FishStrategyCatalogFile();
        }
    }
}
