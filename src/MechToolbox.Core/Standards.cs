using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MechToolbox.Core;

public record ModuleDescriptor(string Id, string Name, string Description, int ApiVersion = 1);

// Pure domain contract: new standards do not depend on WPF or the bolt schema.
public interface IStandardModule
{
    ModuleDescriptor Descriptor { get; }
}

public interface IStandardDataModule : IStandardModule
{
    IReadOnlyList<StandardSource> Sources { get; }
    string DataSummary { get; }
}

public interface IStandardDataProvider<T>
{
    T Load(string path);
}

public sealed class ModuleRegistry
{
    private readonly List<IStandardModule> modules = [];
    public IReadOnlyList<IStandardModule> Modules => modules.AsReadOnly();
    public void Register(IStandardModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        if (module.Descriptor.ApiVersion != 1 || string.IsNullOrWhiteSpace(module.Descriptor.Id))
            throw new ArgumentException("模块接口版本或标识无效。");
        if (modules.Any(x => x.Descriptor.Id == module.Descriptor.Id))
            throw new ArgumentException($"模块标识重复：{module.Descriptor.Id}");
        modules.Add(module);
    }
}

public sealed record StandardSource(string Id, string Title, string Url, string Scope);
public sealed record BoltSize(decimal Diameter, decimal Pitch, decimal TapDrill,
    decimal Fine, decimal Medium, decimal Coarse, string? TapSourceId = null)
{
    public string Spec => $"M{Format.Number(Diameter)}";
    public string ThreadCallout => $"{Spec}-6H";
    public decimal ApproximateDrill => Diameter - Pitch;
    public string TapText => Format.Diameter(TapDrill);
    public string FineText => Format.Diameter(Fine);
    public string MediumText => Format.Diameter(Medium);
    public string CoarseText => Format.Diameter(Coarse);
}

public sealed record BoltDataset(int SchemaVersion, string DataVersion, string UpdatedOn,
    string Title, string ThreadSeries, string ClearanceSourceId, string TapSourceId,
    IReadOnlyList<StandardSource> Sources, IReadOnlyList<BoltSize> Sizes);

public static class Format
{
    public static string Number(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    public static string Diameter(decimal value) => $"⌀{Number(value)}";
}

public sealed class JsonBoltDataProvider : IStandardDataProvider<BoltDataset>
{
    public BoltDataset Load(string path) => Parse(File.ReadAllText(path, Encoding.UTF8));

    public BoltDataset Parse(string json)
    {
        var data = JsonSerializer.Deserialize<BoltDataset>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        }) ?? throw new InvalidDataException("标准数据为空。");
        if (data.SchemaVersion != 1) throw new InvalidDataException("不支持此标准数据结构版本。");
        if (data.ThreadSeries != "metric-coarse") throw new InvalidDataException("本模块仅支持公制粗牙。");
        if (string.IsNullOrWhiteSpace(data.DataVersion) || string.IsNullOrWhiteSpace(data.Title) ||
            !DateOnly.TryParseExact(data.UpdatedOn, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            throw new InvalidDataException("数据版本、标题或更新日期缺失或无效。");
        if (data.Sizes is null || data.Sizes.Count == 0 || data.Sources is null || data.Sources.Count == 0)
            throw new InvalidDataException("规格或标准来源缺失。");
        if (data.Sources.Any(s => s is null || string.IsNullOrWhiteSpace(s.Id) || string.IsNullOrWhiteSpace(s.Title) ||
            string.IsNullOrWhiteSpace(s.Scope) || !Uri.TryCreate(s.Url, UriKind.Absolute, out var uri) || uri.Scheme != "https") ||
            data.Sources.Select(x => x.Id).Distinct().Count() != data.Sources.Count ||
            !data.Sources.Any(x => x.Id == data.ClearanceSourceId) || !data.Sources.Any(x => x.Id == data.TapSourceId))
            throw new InvalidDataException("标准来源标识、说明或 HTTPS 链接无效。");
        if (data.Sizes.Any(s => s is null) || data.Sizes.Select(s => s.Diameter).Distinct().Count() != data.Sizes.Count)
            throw new InvalidDataException("存在空规格或重复规格。");
        foreach (var s in data.Sizes)
        {
            if (s.TapSourceId is not null && !data.Sources.Any(x => x.Id == s.TapSourceId))
                throw new InvalidDataException($"{s.Spec} 底孔来源标识无效。");
            if (s.Diameter <= 0 || s.Pitch <= 0 || s.Pitch >= s.Diameter || s.TapDrill <= 0 || s.TapDrill >= s.Diameter ||
                s.Fine <= s.Diameter || s.Medium <= s.Fine || s.Coarse <= s.Medium)
                throw new InvalidDataException($"{s.Spec} 数据无效：检查螺距、底孔和通孔的尺寸顺序。");
        }
        return data with { Sizes = data.Sizes.OrderBy(s => s.Diameter).ToArray() };
    }
}

public enum ClearanceSeries { Fine, Medium, Coarse }
public sealed record ClearanceResult(ClearanceSeries Series, string Name, string EnglishName,
    decimal Diameter, decimal DiametralClearance, string ToleranceReference, string UseCase)
{
    public string SizeText => Format.Diameter(Diameter);
    public string GapText => $"名义直径间隙 {Format.Number(DiametralClearance)} mm";
    public SlotTolerance Tolerance => LinearTolerances.Hole(Diameter, ToleranceReference);
    public string DeviationText => $"{ToleranceReference}：{Tolerance.Text} mm";
    public string LimitsText => $"允许孔径：{Tolerance.Limits(Diameter)}";
}

public sealed class BoltModule(BoltDataset data, SocketHeadDataset? heads = null) : IStandardDataModule
{
    public ModuleDescriptor Descriptor { get; } = new("bolt-holes", "螺纹孔与通孔", "公制粗牙 · 底孔与装配通孔查询");
    public BoltDataset Data { get; } = data;
    public SocketHeadDataset? Heads { get; } = heads;
    public SocketHeadSize? HeadFor(BoltSize size) => Heads?.Sizes.SingleOrDefault(x => x.Diameter == size.Diameter);
    public IReadOnlyList<StandardSource> Sources => Heads is null ? [..Data.Sources, LinearTolerances.Source, MetricThreadLimits.IsoSource, MetricThreadLimits.Source] : [.. Data.Sources, Heads.Source, LinearTolerances.Source, MetricThreadLimits.IsoSource, MetricThreadLimits.Source];
    public string DataSummary => $"{Data.Title}\n数据版本 {Data.DataVersion} · 核对日期 {Data.UpdatedOn}\n收录 {Data.Sizes.Count} 个规格，{Data.Sizes.First().Spec}—{Data.Sizes.Last().Spec}。通孔按 ISO 273:1979 三个系列提供，底孔采用切削攻丝工艺推荐值。" + (Heads is null ? "" : $"\n内六角圆柱头按 {Heads.Source.Title} 表 1 查询，数据版本 {Heads.DataVersion}，核对日期 {Heads.UpdatedOn}。未列规格不外推。");
    public StandardSource TapSource(BoltSize size) => Data.Sources.Single(x => x.Id == (size.TapSourceId ?? Data.TapSourceId));
    public BoltSize Find(decimal diameter) => Data.Sizes.SingleOrDefault(x => x.Diameter == diameter)
        ?? throw new ArgumentOutOfRangeException(nameof(diameter), "未收录此规格，不能外推尺寸。");
    public IReadOnlyList<ClearanceResult> Clearances(BoltSize size) => [
        new(ClearanceSeries.Fine, "精装配", "FINE", size.Fine, size.Fine - size.Diameter, "H12", "较小装配间隙，适合孔位控制较好的连接。"),
        new(ClearanceSeries.Medium, "中等装配", "MEDIUM", size.Medium, size.Medium - size.Diameter, "H13", "一般机械连接的初始选用，兼顾间隙与装配。"),
        new(ClearanceSeries.Coarse, "粗装配", "COARSE", size.Coarse, size.Coarse - size.Diameter, "H14", "较大装配余量，适合需要放宽装配间隙的连接。")
    ];
    public string HeadSummary(BoltSize size) => HeadFor(size) is { } h
        ? $"内六角圆柱头（GB/T 70.1-2008）：光滑头最大外径 {h.SmoothText}；最大头高 {h.HeightText}；内六角公称对边 {h.SocketText} mm。\n{h.DetailText}" + (size.Diameter == 14 ? " M14 为非优选规格。" : "")
        : Heads is null ? "未加载内六角圆柱头数据。" : $"GB/T 70.1-2008 未列 {size.Spec} 头部尺寸，不外推。";
    public string Summary(BoltSize size, ClearanceSeries series)
    {
        var c = Clearances(size).Single(x => x.Series == series);
        return $"{size.Spec}（公制粗牙，单位 mm）\n" +
            $"内螺纹标注：{size.ThreadCallout}（6H 为一般用途建议）\n{MetricThreadLimits.For(size)?.Summary}\n" +
            $"切削攻丝推荐底孔：{size.TapText}\n" +
            HeadSummary(size) + "\n" +
            string.Join("\n", Clearances(size).Select(x => $"通孔 {x.Name} {x.SizeText}；参考 {x.DeviationText}；{x.LimitsText}")) + "\n" +
            $"当前选用：{c.Name} {c.SizeText}；参考公差带需按图样另行确定\n" +
            "底孔适用于切削丝锥；挤压丝锥须按刀具厂商数据。装配系列不等同于定位精度。\n" +
            $"通孔依据：{Data.Sources.Single(x => x.Id == Data.ClearanceSourceId).Title}\n" +
            $"底孔依据：{TapSource(size).Title}\n线性偏差依据：ISO 286-2:2010 表 1；6H 依据：ISO 965-2:2024 表 1 / BETEC 表 4\n数据版本：{Data.DataVersion}" +
            (Heads is null ? "" : $"\n头部数据版本：{Heads.DataVersion}");
    }
    public string ExportCsv(IEnumerable<BoltSize> sizes)
    {
        var b = new StringBuilder("规格,内螺纹建议标注,切削攻丝底孔_mm,精装配通孔_mm,中等装配通孔_mm,粗装配通孔_mm,光滑头最大外径_mm,滚花头最大外径_mm,最大头高_mm,内六角公称对边_mm,内六角最小深度_mm,头部标准,头部数据版本,通孔标准,底孔依据,数据版本,精装配上偏差_mm,精装配下偏差_mm,精装配最小孔径_mm,精装配最大孔径_mm,中等装配上偏差_mm,中等装配下偏差_mm,中等装配最小孔径_mm,中等装配最大孔径_mm,粗装配上偏差_mm,粗装配下偏差_mm,粗装配最小孔径_mm,粗装配最大孔径_mm,6H中径最小_mm,6H中径最大_mm,6H小径最小_mm,6H小径最大_mm\r\n");
        foreach (var s in sizes)
        {
            var h = HeadFor(s);
            string[] cells = [s.Spec, s.ThreadCallout, Format.Number(s.TapDrill),
                Format.Number(s.Fine), Format.Number(s.Medium), Format.Number(s.Coarse),
                h is null ? "" : Format.Number(h.SmoothDiameterMax), h is null ? "" : Format.Number(h.KnurledDiameterMax),
                h is null ? "" : Format.Number(h.HeightMax), h is null ? "" : Format.Number(h.SocketNominal),
                h is null ? "" : Format.Number(h.SocketDepthMin), h is null ? "" : Heads!.Source.Title, h is null ? "" : Heads!.DataVersion,
                Data.Sources.Single(x => x.Id == Data.ClearanceSourceId).Title,
                TapSource(s).Title, Data.DataVersion];
            var limits = MetricThreadLimits.For(s);
            var extras = Clearances(s).SelectMany(x => new[] { KeyFormat.Number(x.Tolerance.Upper), "0", KeyFormat.Number(x.Diameter), KeyFormat.Number(x.Diameter+x.Tolerance.Upper) });
            string[] thread = limits is null ? ["", "", "", ""] : [KeyFormat.Number(limits.PitchMin), KeyFormat.Number(limits.PitchMax), KeyFormat.Number(limits.MinorMin), KeyFormat.Number(limits.MinorMax)];
            b.AppendLine(string.Join(",", cells.Concat(extras).Concat(thread).Select(CsvCell)));
        }
        return b.ToString();
    }
    private static string CsvCell(string value)
    {
        // Keep future enterprise source titles from becoming Excel formulas.
        if (value.TrimStart().StartsWith('=') || value.TrimStart().StartsWith('+') ||
            value.TrimStart().StartsWith('-') || value.TrimStart().StartsWith('@')) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
