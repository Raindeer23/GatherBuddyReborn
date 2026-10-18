using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.RegularExpressions;

if (args.Length != 3)
    throw new ArgumentException("Usage: SyncRegression <plugin-output> <dalamud-directory> <repo-root>");

var output = Path.GetFullPath(args[0]);
var dalamud = Path.GetFullPath(args[1]);
var repo = Path.GetFullPath(args[2]);
AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    foreach (var directory in new[] { output, dalamud })
    {
        var path = Path.Combine(directory, name.Name + ".dll");
        if (File.Exists(path))
            return context.LoadFromAssemblyPath(path);
    }
    return null;
};
var plugin = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(output, "GatherBuddyReborn.dll"));
const BindingFlags hiddenStatic = BindingFlags.NonPublic | BindingFlags.Static;
var passed = 0;
void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
    passed++;
    Console.WriteLine("PASS: " + message);
}

// Exercise the compiled parser without initializing Dalamud or a game client.
var regexType = plugin.GetType("GatherBuddy.FishTimer.Parser.FishingParser+Regexes", true)!;
var fromLanguage = regexType.GetMethod("FromLanguage")!;
var languageType = fromLanguage.GetParameters()[0].ParameterType;
foreach (var (language, sentence, expected) in new[]
{
    (4, "你在测试钓场甩出了鱼线开始钓鱼。", "测试钓场"),
    (Convert.ToInt32(Enum.Parse(languageType, "English")), "You cast your line at Test Lake.", "Test Lake"),
})
{
    var regexes = fromLanguage.Invoke(null, [Enum.ToObject(languageType, language)])!;
    var cast = (Regex)regexType.GetProperty("Cast")!.GetValue(regexes)!;
    Check(cast.Match(sentence).Groups["FishingSpot"].Value == expected, $"Fishing site parser, language {language}");
}

var tokensMethod = plugin.GetType("GatherBuddy.Gui.FishStrategyTextRenderer", true)!
    .GetMethod("Tokens", hiddenStatic)!;
var tokens = ((IEnumerable)tokensMethod.Invoke(null, ["7.5 [!] [!!] [!!!] <雄心> [完美的碟子] 20:00"])!)
    .Cast<string>().Where(token => !string.IsNullOrWhiteSpace(token)).ToArray();
Check(tokens.SequenceEqual(new[] { "7.5", "[!]", "[!!]", "[!!!]", "<雄心>", "[完美的碟子]", "20:00" }),
    "Strategy words, times and tug markers remain indivisible");

using (var resource = plugin.GetManifestResourceStream("GatherBuddy.CustomInfo.fish_strategies.zh-CN.json"))
{
    Check(resource != null, "Chinese strategy catalog is embedded in the main assembly");
    using var reader = new StreamReader(resource!);
    var embedded = reader.ReadToEnd();
    var source = File.ReadAllText(Path.Combine(repo, "GatherBuddy/CustomInfo/fish_strategies.zh-CN.json"));
    Check(embedded == source, "Embedded strategies match source exactly");
    using var catalog = JsonDocument.Parse(embedded);
    Check(catalog.RootElement.GetProperty("strategies").EnumerateObject().Count() == 46, "All 46 strategies remain present");
}

var recordType = plugin.GetType("GatherBuddy.FishTimer.FishRecord", true)!;
Check(recordType.IsClass, "Reborn record storage remains a class");
var record = Activator.CreateInstance(recordType)!;
var setTugHook = recordType.GetMethod("SetTugHook")!;
var parameters = setTugHook.GetParameters();
setTugHook.Invoke(record, [
    Enum.Parse(parameters[0].ParameterType, "鱼王竿"),
    Enum.Parse(parameters[1].ParameterType, "Precise"),
]);
var share = (ITuple)recordType.GetMethod("ToSharable", BindingFlags.NonPublic | BindingFlags.Instance)!
    .Invoke(record, null)!;
var catchData = (ITuple)share[0]!;
var castData = (ITuple)share[1]!;
Check((byte)catchData[4]! == 3 && (byte)castData[6]! == 2, "IPC tug and hook fields are not swapped");

var ipcType = plugin.GetType("GatherBuddy.Plugin.GatherBuddyIpc", true)!;
Check((int)ipcType.GetField("IpcVersion")!.GetRawConstantValue()! == 2, "Reborn IPC major version is preserved");
foreach (var method in new[] { "Version", "Identify", "IsAutoGatherEnabled", "GetAutoGatherStatusText", "SetAutoGatherEnabled", "IsAutoGatherWaiting" })
    Check(ipcType.GetMethod(method)!.CustomAttributes.Any(a => a.AttributeType.Name == "EzIPCAttribute"),
        "Existing IPC endpoint remains registered: " + method);

var gameData = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(output, "GatherBuddy.GameData.dll"));
var stampType = gameData.GetType("GatherBuddy.Time.TimeStamp", true)!;
var intervalType = gameData.GetType("GatherBuddy.Time.TimeInterval", true)!;
var duration = intervalType.GetMethod("DurationString", [stampType, stampType, typeof(bool)])!;
foreach (var (milliseconds, unit) in new[] { (60000L, "分钟"), (7200000L, "小时"), (172800000L, "天") })
{
    var formatted = (string)duration.Invoke(null, [
        Activator.CreateInstance(stampType, [0L]),
        Activator.CreateInstance(stampType, [milliseconds]),
        false,
    ])!;
    Check(formatted.Contains(unit), "Chinese duration label: " + unit);
}
Console.WriteLine($"{passed} checks passed.");
