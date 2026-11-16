using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MechToolbox.Core;

public static class KeyFormat
{
    public static string Number(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);
    public static string Signed(decimal value) => value > 0 ? "+" + Number(value) : Number(value);
    public static string Deviation(decimal upper, decimal lower) => $"{Signed(upper)} / {Signed(lower)}";
}

public sealed record SlotTolerance(decimal Lower, decimal Upper)
{
    public string Text => KeyFormat.Deviation(Upper, Lower);
    public string Limits(decimal width) => $"{KeyFormat.Number(width + Lower)} ～ {KeyFormat.Number(width + Upper)} mm";
}

public sealed record KeywaySize(decimal ShaftMin, decimal ShaftMax, decimal Width, decimal Height,
    decimal ShaftDepth, decimal HubDepth, decimal DepthUpper, decimal RadiusMin, decimal RadiusMax,
    SlotTolerance N9, SlotTolerance Js9, SlotTolerance P9, SlotTolerance H9, SlotTolerance D10)
{
    public string Section => $"{KeyFormat.Number(Width)} × {KeyFormat.Number(Height)}";
    public string RangeText => $"{(ShaftMin == 6 ? "≥" : ">")}{KeyFormat.Number(ShaftMin)}，≤{KeyFormat.Number(ShaftMax)}";
    public string RadiusText => $"{KeyFormat.Number(RadiusMin)} ～ {KeyFormat.Number(RadiusMax)}";
    public string DepthDeviation => KeyFormat.Deviation(DepthUpper, 0);
}

public sealed record KeywayDataset(int SchemaVersion, string DataVersion, string UpdatedOn,
    string Title, string Standard, string SizeSourceId, string ShaftSelectionSourceId,
    IReadOnlyList<StandardSource> Sources, IReadOnlyList<KeywaySize> Sizes);

public sealed class JsonKeywayDataProvider : IStandardDataProvider<KeywayDataset>
{
    public KeywayDataset Load(string path) => Parse(File.ReadAllText(path, Encoding.UTF8));
    public KeywayDataset Parse(string json)
    {
        var data = JsonSerializer.Deserialize<KeywayDataset>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        }) ?? throw new InvalidDataException("键槽数据为空。");
        if (data.SchemaVersion != 1 || data.Standard != "GB/T 1095-2003") throw new InvalidDataException("键槽数据的结构版本或标准无效。");
        if (string.IsNullOrWhiteSpace(data.DataVersion) || string.IsNullOrWhiteSpace(data.Title) ||
            !DateOnly.TryParseExact(data.UpdatedOn, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            throw new InvalidDataException("键槽数据的版本、标题或核对日期无效。");
        if (data.Sizes is null || data.Sizes.Count == 0 || data.Sources is null || data.Sources.Count == 0 ||
            data.Sources.Any(x => x is null || string.IsNullOrWhiteSpace(x.Id) || string.IsNullOrWhiteSpace(x.Title) ||
                string.IsNullOrWhiteSpace(x.Scope) || !Uri.TryCreate(x.Url, UriKind.Absolute, out var uri) || uri.Scheme != "https") ||
            data.Sources.Select(x => x.Id).Distinct().Count() != data.Sources.Count ||
            !data.Sources.Any(x => x.Id == data.SizeSourceId) || !data.Sources.Any(x => x.Id == data.ShaftSelectionSourceId))
            throw new InvalidDataException("键槽规格或来源无效。");
        if (data.Sizes.Any(x => x is null)) throw new InvalidDataException("键槽数据存在空行。");
        var sizes = data.Sizes.OrderBy(x => x.ShaftMin).ToArray();
        decimal end = 6;
        decimal previousWidth = 0;
        foreach (var s in sizes)
        {
            SlotTolerance[] tolerances = [s.N9, s.Js9, s.P9, s.H9, s.D10];
            if (s.ShaftMin != end || s.ShaftMax <= s.ShaftMin || s.Width <= previousWidth ||
                s.Height <= 0 || s.ShaftDepth <= 0 || s.ShaftDepth >= s.Height || s.HubDepth <= 0 ||
                s.ShaftDepth + s.HubDepth <= s.Height || s.DepthUpper <= 0 ||
                s.RadiusMin <= 0 || s.RadiusMax < s.RadiusMin ||
                tolerances.Any(t => t is null || t.Lower >= t.Upper || s.Width + t.Lower <= 0))
                throw new InvalidDataException("键槽尺寸、推荐轴径区间或公差无效。");
            if (s.N9.Upper > 0 || s.P9.Upper >= 0 || s.H9.Lower != 0 ||
                s.D10.Lower <= 0 || s.Js9.Upper != -s.Js9.Lower)
                throw new InvalidDataException("键槽公差带方向不符合定义。");
            end = s.ShaftMax; previousWidth = s.Width;
        }
        return data with { Sizes = sizes };
    }
}

public enum KeywayFit { Normal, Tight, Loose }
public sealed record KeywayFitOption(KeywayFit Fit, string Name, string ShaftBand, string HubBand);
public sealed record KeywayResult(decimal ShaftDiameter, KeywaySize Size, KeywayFitOption Fit,
    SlotTolerance ShaftWidthTolerance, SlotTolerance HubWidthTolerance)
{
    public string DiameterText => $"⌀{KeyFormat.Number(ShaftDiameter)} mm";
    public string SectionText => Size.Section + " mm";
    public string RangeText => $"推荐轴径档：{Size.RangeText} mm";
    public string ShaftWidth => $"{KeyFormat.Number(Size.Width)} {Fit.ShaftBand}";
    public string HubWidth => $"{KeyFormat.Number(Size.Width)} {Fit.HubBand}";
    public string ShaftWidthDeviation => $"上 / 下偏差：{ShaftWidthTolerance.Text} mm";
    public string HubWidthDeviation => $"上 / 下偏差：{HubWidthTolerance.Text} mm";
    public string ShaftWidthLimits => $"极限尺寸：{ShaftWidthTolerance.Limits(Size.Width)}";
    public string HubWidthLimits => $"极限尺寸：{HubWidthTolerance.Limits(Size.Width)}";
    public string ShaftDepth => $"{KeyFormat.Number(Size.ShaftDepth)} mm";
    public string HubDepth => $"{KeyFormat.Number(Size.HubDepth)} mm";
    public string DepthDeviation => $"上 / 下偏差：{Size.DepthDeviation} mm";
    public decimal ShaftInspection => ShaftDiameter - Size.ShaftDepth;
    public decimal HubInspection => ShaftDiameter + Size.HubDepth;
    public string ShaftInspectionText => $"{KeyFormat.Number(ShaftInspection)} mm";
    public string HubInspectionText => $"{KeyFormat.Number(HubInspection)} mm";
    public string ShaftInspectionDeviation => $"上 / 下偏差：{KeyFormat.Deviation(0, -Size.DepthUpper)} mm";
    public string HubInspectionDeviation => DepthDeviation;
    public string ShaftDepthLimits => $"极限尺寸：{new SlotTolerance(0, Size.DepthUpper).Limits(Size.ShaftDepth)}";
    public string HubDepthLimits => $"极限尺寸：{new SlotTolerance(0, Size.DepthUpper).Limits(Size.HubDepth)}";
    public string ShaftInspectionLimits => $"极限尺寸：{new SlotTolerance(-Size.DepthUpper, 0).Limits(ShaftInspection)}";
    public string HubInspectionLimits => $"极限尺寸：{new SlotTolerance(0, Size.DepthUpper).Limits(HubInspection)}";
    public string RadiusText => Size.RadiusText + " mm";
    public string TopClearance => $"{KeyFormat.Number(Size.ShaftDepth + Size.HubDepth - Size.Height)} mm（名义值）";
}

public sealed class KeywayModule(KeywayDataset data) : IStandardDataModule
{
    public ModuleDescriptor Descriptor { get; } = new("keyways", "键与键槽", "普通平键 · 轴槽与轮毂槽查询");
    public KeywayDataset Data { get; } = data;
    public IReadOnlyList<StandardSource> Sources => Data.Sources;
    public string DataSummary => $"{Data.Title}\n数据版本 {Data.DataVersion} · 核对日期 {Data.UpdatedOn}\n收录 {Data.Sizes.Count} 档键截面。槽宽、槽深和圆角按 {Data.Standard} 表 1 核对；轴径选键属于设计推荐。";
    public static IReadOnlyList<KeywayFitOption> Fits { get; } = Array.AsReadOnly<KeywayFitOption>([
        new(KeywayFit.Normal, "正常联结", "N9", "JS9"),
        new(KeywayFit.Tight, "紧密联结", "P9", "P9"),
        new(KeywayFit.Loose, "松联结", "H9", "D10")
    ]);
    public KeywayResult Query(decimal diameter, KeywayFit fit)
    {
        var row = Data.Sizes.FirstOrDefault(x => (diameter > x.ShaftMin || diameter == 6 && x.ShaftMin == 6) && diameter <= x.ShaftMax)
            ?? throw new ArgumentOutOfRangeException(nameof(diameter), "轴径不在收录的推荐范围内。");
        var option = Fits.SingleOrDefault(x => x.Fit == fit) ?? throw new ArgumentOutOfRangeException(nameof(fit));
        return new(diameter, row, option,
            fit switch { KeywayFit.Normal => row.N9, KeywayFit.Tight => row.P9, _ => row.H9 },
            fit switch { KeywayFit.Normal => row.Js9, KeywayFit.Tight => row.P9, _ => row.D10 });
    }
    public string Summary(KeywayResult r) =>
        $"普通平键 · {r.DiameterText} · {r.Fit.Name}\n键截面 b×h：{r.SectionText}\n{r.RangeText}\n" +
        $"轴槽宽：{r.ShaftWidth}；{r.ShaftWidthDeviation}；{r.ShaftWidthLimits}\n" +
        $"毂槽宽：{r.HubWidth}；{r.HubWidthDeviation}；{r.HubWidthLimits}\n" +
        $"轴槽深 t₁：{r.ShaftDepth}；{r.DepthDeviation}；{r.ShaftDepthLimits}\n轮毂槽深 t₂：{r.HubDepth}；{r.DepthDeviation}；{r.HubDepthLimits}\n" +
        $"轴槽检验尺寸 d−t₁：{r.ShaftInspectionText}；{r.ShaftInspectionDeviation}；{r.ShaftInspectionLimits}\n" +
        $"毂槽检验尺寸 d+t₂：{r.HubInspectionText}；{r.HubInspectionDeviation}；{r.HubInspectionLimits}\n" +
        $"槽底圆角 r：{r.RadiusText}\n名义顶隙：{r.TopClearance}\n" +
        $"截面尺寸与槽宽公差：{Data.Standard} 表 1；轴径选键属于设计推荐。\n" +
        $"推荐来源：{Data.Sources.Single(x => x.Id == Data.ShaftSelectionSourceId).Title}\n" +
        $"数据版本：{Data.DataVersion}。键长需按轮毂长度与载荷另行设计。";
}
