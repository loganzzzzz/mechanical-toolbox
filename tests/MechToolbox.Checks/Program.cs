using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using MechToolbox.Core;

var provider = new JsonBoltDataProvider();
var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "metric-coarse.v1.json"));
var data = provider.Parse(json);
var headJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "socket-head-gbt70-1.v1.json"));
var headProvider = new JsonSocketHeadDataProvider();
var heads = headProvider.Parse(headJson);
var module = new BoltModule(data, heads);
int count = 0;
void Check(bool success, string name) { if (!success) throw new Exception($"FAIL: {name}"); count++; }
void Reject(Action action, string name)
{
    try { action(); }
    catch (Exception ex) when (ex is InvalidDataException or JsonException or ArgumentException) { count++; return; }
    throw new Exception($"FAIL: {name} should have been rejected");
}
void BadData(Action<JsonObject> mutate, string name)
{
    var node = JsonNode.Parse(json)!.AsObject(); mutate(node);
    Reject(() => provider.Parse(node.ToJsonString()), name);
}

// Approved fixture is reviewed against the cited source tables, independently of the JSON loader.
var approved = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "approved-sizes.csv")).Skip(1)
    .Where(x => !string.IsNullOrWhiteSpace(x)).Select(line => line.Split(',').Select(x => decimal.Parse(x, CultureInfo.InvariantCulture)).ToArray()).ToArray();
Check(data.Sizes.Count == approved.Length, "complete supported size range");
foreach (var row in approved)
{
    var s = module.Find(row[0]);
    Check(s.Pitch == row[1] && s.TapDrill == row[2] && s.Fine == row[3] && s.Medium == row[4] && s.Coarse == row[5], $"source values M{row[0]}");
    var clearances = module.Clearances(s);
    Check(clearances.Count == 3 && clearances.All(c => c.DiametralClearance == c.Diameter - s.Diameter), "diametral clearance");
}
Check(module.Find(8).ApproximateDrill == 6.75m && module.Find(8).TapDrill == 6.8m, "recommended drill must not be blindly replaced by d-P");
Check(module.Find(12).TapDrill == 10.2m && module.Find(12).Coarse == 14.5m, "M12 critical values");
Check(module.Summary(module.Find(8), ClearanceSeries.Coarse).Contains("粗装配 ⌀10"), "copy selected clearance");
Check(module.Summary(module.Find(6), ClearanceSeries.Fine).Contains("M6-6H"), "coarse thread callout omits pitch");
Check(module.Summary(module.Find(2), ClearanceSeries.Medium).Contains("Gühring · Series 815"), "small size carries its own drill source");
var csv = module.ExportCsv([module.Find(8)]);
Check(csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length == 2 && csv.Contains("\"6.8\",\"8.4\",\"9\",\"10\""), "filtered CSV exports numeric mm");
foreach (var size in data.Sizes)
{
    Check(size.ThreadCallout == $"{size.Spec}-6H", "all coarse callouts omit pitch");
    var summary = module.Summary(size, ClearanceSeries.Medium);
    Check(summary.StartsWith($"{size.Spec}（公制粗牙") && !summary.Contains('×') && !summary.Contains("螺距"), "copy output omits pitch");
}
Check(!csv.Contains("螺距") && !csv.Contains('×') && csv.Contains("\"M8-6H\""), "CSV API omits pitch");
var previous = CultureInfo.CurrentCulture;
CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
Check(module.Find(8).Spec == "M8" && module.ExportCsv([module.Find(8)]) == csv, "decimal separator independent of Windows locale");
CultureInfo.CurrentCulture = previous;
Reject(() => module.Find(9), "no extrapolation of unsupported sizes");
BadData(n => n["schemaVersion"] = 2, "future schema");
BadData(n => n.Remove("schemaVersion"), "missing schema");
BadData(n => n["threadSeries"] = "metric-fine", "wrong thread series");
BadData(n => n["updatedOn"] = "invalid", "invalid revision date");
BadData(n => n["sizes"] = new JsonArray(), "empty sizes");
BadData(n => n["sizes"] = null, "null sizes");
BadData(n => n["sizes"]![0] = null, "null row");
BadData(n => n["sizes"]!.AsArray().Add(n["sizes"]![0]!.DeepClone()), "duplicate diameter");
BadData(n => n["sizes"]![0]!["pitch"] = 0, "zero pitch");
BadData(n => n["sizes"]![0]!["tapDrill"] = 2, "drill larger than thread");
BadData(n => n["sizes"]![0]!["fine"] = 1, "clearance smaller than bolt");
BadData(n => n["sizes"]![0]!["medium"] = 1.7, "non-increasing clearance series");
BadData(n => n["clearanceSourceId"] = "missing", "dangling source");
BadData(n => n["sizes"]![0]!["tapSourceId"] = "missing", "dangling row source");
BadData(n => n["sources"]![0]!["url"] = "file:///tmp/a", "invalid source URL");
BadData(n => n["unrecognized"] = 1, "unknown fields must not silently load");
var registry = new ModuleRegistry(); registry.Register(module);
Reject(() => registry.Register(module), "duplicate module");
registry.Register(new StubModule(new("keys", "键槽", "independent standard")));
Check(registry.Modules.Count == 2, "independent standard registers without bolt changes");
Reject(() => registry.Register(new StubModule(new("new", "future", "", 2))), "future module API");
var headRows = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "approved-socket-heads.csv")).Skip(1)
    .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Split(',').Select(v => decimal.Parse(v, CultureInfo.InvariantCulture)).ToArray()).ToArray();
Check(heads.Sizes.Count == headRows.Length, "complete GB/T 70.1 table 1 head rows");
foreach (var row in headRows)
{
    var h = heads.Sizes.Single(x => x.Diameter == row[0]);
    Check(h.SmoothDiameterMax == row[1] && h.KnurledDiameterMax == row[2] && h.HeightMax == row[3] &&
        h.SocketNominal == row[4] && h.SocketDepthMin == row[5], $"GB/T 70.1 original table M{row[0]}");
}
Check(module.HeadFor(module.Find(8)) is { SmoothDiameterMax: 13m, KnurledDiameterMax: 13.27m, HeightMax: 8m, SocketNominal: 6m, SocketDepthMin: 4m }, "M8 smooth/knurled distinction");
Check(module.HeadFor(module.Find(36)) is { SmoothDiameterMax: 54m, KnurledDiameterMax: 54.46m, SocketNominal: 27m }, "M36 head dimensions");
foreach (var size in data.Sizes)
{
    var h = module.HeadFor(size);
    Check((h is not null) == headRows.Any(x => x[0] == size.Diameter), "head availability by exact nominal diameter");
    if (h is null) Check(module.HeadSummary(size).Contains("未列") && !module.ExportCsv([size]).Contains("⌀"), "unsupported head size not extrapolated");
}
Check(module.HeadSummary(module.Find(14)).Contains("非优选"), "M14 non-preferred standard footnote");
Check(module.Summary(module.Find(8), ClearanceSeries.Medium).Contains("光滑头最大外径 ⌀13") &&
    module.Summary(module.Find(8), ClearanceSeries.Medium).Contains("13.27"), "copy output includes head values");
Check(module.ExportCsv([module.Find(8)]).Contains("\"13\",\"13.27\",\"8\",\"6\",\"4\""), "CSV head dimensions");
foreach (var bad in new Action<JsonObject>[] {
    n => n["schemaVersion"] = 2, n => n["updatedOn"] = "invalid", n => n["source"] = null,
    n => n["source"]!["url"] = "file:///tmp/a", n => n["sizes"] = null, n => n["sizes"] = new JsonArray(),
    n => n["sizes"]![0] = null, n => n["sizes"]!.AsArray().Add(n["sizes"]![0]!.DeepClone()),
    n => n["sizes"]![0]!["smoothDiameterMax"] = 1, n => n["sizes"]![0]!["knurledDiameterMax"] = 1,
    n => n["sizes"]![0]!["heightMax"] = 0, n => n["sizes"]![0]!["socketNominal"] = 0,
    n => n["sizes"]![0]!["socketDepthMin"] = 10, n => n["unknown"] = 1 })
{
    var node = JsonNode.Parse(headJson)!.AsObject(); bad(node);
    Reject(() => headProvider.Parse(node.ToJsonString()), "reject corrupt socket head dataset");
}
NewModuleChecks.Run(module, Check, Reject);
CirclipChecks.Run(Check, Reject);
ToleranceChecks.Run(module, Check, Reject);
Console.WriteLine($"PASS: {count} checks; {approved.Length} bolt sizes; 26 keyway sections; 174 circlip rows; forward/reverse queries and invalid input.");

sealed class StubModule(ModuleDescriptor descriptor) : IStandardModule { public ModuleDescriptor Descriptor => descriptor; }
