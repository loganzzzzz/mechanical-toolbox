using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MechToolbox.Core;

public enum CirclipKind { Shaft, Bore }
public sealed record CirclipKindOption(CirclipKind Kind, string Name);
public sealed record CirclipSize(CirclipKind Kind, decimal Diameter, decimal Thickness, decimal FreeDiameter,
    decimal GrooveDiameter, decimal GrooveWidth, decimal NominalDepth, decimal EdgeMin,
    SlotTolerance GrooveTolerance, int SourcePage, string ReviewNote)
{
    public bool NeedsReview => !string.IsNullOrEmpty(ReviewNote);
    public string Standard => Kind == CirclipKind.Shaft ? "GB/T 894-2017" : "GB/T 893-2017";
    public string TypeName => Kind == CirclipKind.Shaft ? "轴用弹性挡圈" : "孔用弹性挡圈";
    public string Designation => $"挡圈 {Standard.Replace("-2017", "")} {KeyFormat.Number(Diameter)}";
    public string DiameterText => $"⌀{KeyFormat.Number(Diameter)}";
    public string ThicknessText => KeyFormat.Number(Thickness);
    public string FreeDiameterName => Kind == CirclipKind.Shaft ? "自由内径 d₃ · 基本尺寸" : "自由外径 d₃ · 基本尺寸";
    public string FreeDiameterText => $"⌀{KeyFormat.Number(FreeDiameter)}";
    public string GrooveText => NeedsReview ? "待核对" : $"⌀{KeyFormat.Number(GrooveDiameter)}";
    public string GrooveDeviation => NeedsReview ? "槽径暂不输出" : $"上 / 下偏差：{GrooveTolerance.Text} mm";
    public string GrooveLimits => NeedsReview ? ReviewNote : $"槽径极限：{GrooveTolerance.Limits(GrooveDiameter)}";
    public string WidthText => $"{KeyFormat.Number(GrooveWidth)} H13";
    public SlotTolerance WidthTolerance => LinearTolerances.Hole(GrooveWidth, "H13");
    public string WidthDeviation => $"上 / 下偏差：{WidthTolerance.Text} mm";
    public string WidthLimits => $"槽宽极限：{WidthTolerance.Limits(GrooveWidth)}";
    public string DepthText => NeedsReview ? "待核对" : KeyFormat.Number(NominalDepth);
    public string EdgeText => $"≥ {KeyFormat.Number(EdgeMin)}";
    public string RadiusText => $"≤ {KeyFormat.Number(Thickness * .1m)} mm";
    public string SourceText => $"{Standard} 表 1 · 正文第 {SourcePage} 页 · 标准型 A 型";
}
public sealed record CirclipDataset(int SchemaVersion, string DataVersion, string UpdatedOn, string Title,
    IReadOnlyList<StandardSource> Sources, IReadOnlyList<CirclipSize> Sizes);

public sealed class JsonCirclipDataProvider : IStandardDataProvider<CirclipDataset>
{
    public CirclipDataset Load(string path) => Parse(File.ReadAllText(path));
    public CirclipDataset Parse(string json)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        var d = JsonSerializer.Deserialize<CirclipDataset>(json, options) ?? throw new InvalidDataException("挡圈数据为空。");
        if (d.SchemaVersion != 1 || string.IsNullOrWhiteSpace(d.DataVersion) || string.IsNullOrWhiteSpace(d.Title) ||
            !DateOnly.TryParseExact(d.UpdatedOn, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ||
            d.Sources is null || d.Sources.Count == 0 || d.Sizes is null || d.Sizes.Count == 0 ||
            d.Sources.Any(x => x is null || string.IsNullOrWhiteSpace(x.Id) || string.IsNullOrWhiteSpace(x.Title) || string.IsNullOrWhiteSpace(x.Scope) ||
                !Uri.TryCreate(x.Url, UriKind.Absolute, out var uri) || uri.Scheme != "https") ||
            d.Sources.Select(x => x.Id).Distinct().Count() != d.Sources.Count || !d.Sources.Any(x => x.Id == "gb893") || !d.Sources.Any(x => x.Id == "gb894") ||
            d.Sizes.Any(x => x is null) || d.Sizes.Select(x => (x.Kind,x.Diameter)).Distinct().Count() != d.Sizes.Count)
            throw new InvalidDataException("挡圈数据结构、版本、来源或规格无效。");
        foreach (var x in d.Sizes)
        {
            if (!Enum.IsDefined(x.Kind) || x.Diameter <= 0 || x.Thickness <= 0 || x.FreeDiameter <= 0 || x.GrooveDiameter <= 0 ||
                x.GrooveWidth <= x.Thickness || x.NominalDepth <= 0 || x.EdgeMin <= 0 || x.SourcePage <= 0 || x.ReviewNote is null ||
                x.GrooveTolerance is not { } t || t.Lower >= t.Upper || x.GrooveDiameter + t.Lower <= 0 ||
                (x.Kind == CirclipKind.Shaft ? x.FreeDiameter >= x.GrooveDiameter || x.GrooveDiameter >= x.Diameter || t.Upper != 0 || t.Lower >= 0
                    : x.FreeDiameter <= x.GrooveDiameter || x.GrooveDiameter <= x.Diameter || t.Lower != 0 || t.Upper <= 0) ||
                (!x.NeedsReview && Math.Abs(x.Diameter-x.GrooveDiameter)/2 != x.NominalDepth))
                throw new InvalidDataException($"挡圈 {x.TypeName} {x.DiameterText} 尺寸或偏差方向无效。");
        }
        return d with { Sizes = d.Sizes.OrderBy(x => x.Kind).ThenBy(x => x.Diameter).ToArray() };
    }
}

public sealed class CirclipModule(CirclipDataset data) : IStandardDataModule
{
    public ModuleDescriptor Descriptor { get; } = new("circlips", "卡簧与卡簧槽", "轴用与孔用弹性挡圈 · 标准型 A 型");
    public CirclipDataset Data { get; } = data;
    public IReadOnlyList<StandardSource> Sources => [..Data.Sources, LinearTolerances.Source];
    public string DataSummary => $"{Data.Title}\n数据版本 {Data.DataVersion} · 核对日期 {Data.UpdatedOn}\n轴用 {Sizes(CirclipKind.Shaft).Count} 档，孔用 {Sizes(CirclipKind.Bore).Count} 档；按各标准表 1 精确匹配公称轴径／孔径，不插值。轴用 40 mm 的原表槽径与槽深矛盾，已屏蔽这两个加工值并提示核对。";
    public IReadOnlyList<CirclipSize> Sizes(CirclipKind kind) => Data.Sizes.Where(x => x.Kind == kind).ToArray();
    public CirclipSize Query(CirclipKind kind, decimal diameter) => Data.Sizes.SingleOrDefault(x => x.Kind == kind && x.Diameter == diameter)
        ?? throw new ArgumentOutOfRangeException(nameof(diameter), "未收录该公称规格。");
    public string Summary(CirclipSize x) => $"{x.TypeName} · 标准型 A 型 · 单位 mm\n{x.Designation}\n公称轴径／孔径 d₁：{x.DiameterText}\n" +
        $"卡簧厚度 s（基本尺寸）：{x.ThicknessText}\n{x.FreeDiameterName}：{x.FreeDiameterText}\n" +
        $"槽径 d₂：{x.GrooveText}；{x.GrooveDeviation}\n{x.GrooveLimits}\n槽宽 m：{x.WidthText}；{x.WidthDeviation}\n{x.WidthLimits}\n" +
        $"名义槽深 t：{x.DepthText}\n最小端部边距 n：{x.EdgeText}\n受载边槽底圆角 r：{x.RadiusText}\n" +
        $"{x.SourceText}；第 6.3 条圆角要求；槽宽偏差按 ISO 286-2:2010 表 1\n边距须结合载荷与材料复核，本页不计算承载力或极限转速。\n数据版本：{Data.DataVersion}";
}

public sealed class CirclipViewModel : INotifyPropertyChanged
{
    public static IReadOnlyList<CirclipKindOption> Kinds { get; } = Array.AsReadOnly<CirclipKindOption>([
        new(CirclipKind.Shaft,"轴用弹性挡圈"), new(CirclipKind.Bore,"孔用弹性挡圈")]);
    public CirclipModule Module { get; }
    private CirclipKindOption selectedKind = Kinds[0];
    private string diameterText = "30";
    public CirclipSize? Result { get; private set; }
    public string Error { get; private set; } = "";
    public bool HasResult => Result is not null;
    public IReadOnlyList<decimal> Diameters => Module.Sizes(selectedKind.Kind).Select(x => x.Diameter).ToArray();
    public string InputLabel => selectedKind.Kind == CirclipKind.Shaft ? "公称轴径 d₁（mm）" : "公称孔径 d₁（mm）";
    public string SupportedRange => $"标准型 A 型 · {KeyFormat.Number(Diameters.First())}～{KeyFormat.Number(Diameters.Last())} mm，共 {Diameters.Count} 档。输入公称规格或从列表选择，不按实测直径自动套用。";
    public CirclipViewModel(CirclipModule module) { Module = module; Recalculate(); }
    public CirclipKindOption SelectedKind
    {
        get => selectedKind;
        set { if (value is null || !Kinds.Contains(value)) return; selectedKind = value; Notify(); Notify(nameof(Diameters)); Notify(nameof(InputLabel)); Notify(nameof(SupportedRange)); Recalculate(); }
    }
    public string DiameterText { get => diameterText; set { diameterText = value ?? ""; Notify(); Recalculate(); } }
    private void Recalculate()
    {
        Result = null;
        if (!decimal.TryParse(diameterText.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var d) || d <= 0 || decimal.Round(d,3) != d)
            Error = "请输入有效公称轴径或孔径，最多 3 位小数。";
        else
        {
            try { Result = Module.Query(selectedKind.Kind,d); Error = ""; }
            catch (ArgumentOutOfRangeException) { Error = "未收录此公称规格，请从列表选择；不插值或套用相邻规格。"; }
        }
        Notify(nameof(Result)); Notify(nameof(HasResult)); Notify(nameof(Error));
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(name));
}
