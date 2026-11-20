namespace MechToolbox.Core;

/// <summary>GB/T 1800.2-2020 tables 1 and 6 (printed pages 3 and 10). Basic size ranges are open below, closed above; values are mm.</summary>
public static class LinearTolerances
{
    public static StandardSource Source { get; } = new("gb1800-2", "GB/T 1800.2-2020 · 表 1、表 6",
        "https://openstd.samr.gov.cn/bzgk/std/newGbInfo?hcno=8450464EA932756AE6C10593E68AA1AF", "2026-11-20 官方题录确认为现行；表 1 正文第 3 页、表 6 正文第 10 页。H12、H13、H14 数值偏差按实际孔径或槽宽分档，H 下偏差为 0；本版数值表适用大于 0 至 50 mm。既适用于孔径，也适用于槽的两平行表面间距。");
    private static readonly (decimal Max, decimal It12, decimal It13, decimal It14)[] Bands =
    [ (3,.1m,.14m,.25m), (6,.12m,.18m,.3m), (10,.15m,.22m,.36m),
      (18,.18m,.27m,.43m), (30,.21m,.33m,.52m), (50,.25m,.39m,.62m) ];
    public static SlotTolerance Hole(decimal basicSize, string band)
    {
        if (basicSize <= 0 || basicSize > Bands[^1].Max || band == "H14" && basicSize <= 1) throw new ArgumentOutOfRangeException(nameof(basicSize));
        var row = Bands.First(x => basicSize <= x.Max);
        return new(0, band switch { "H12" => row.It12, "H13" => row.It13, "H14" => row.It14,
            _ => throw new ArgumentOutOfRangeException(nameof(band), "未支持的线性公差带。") });
    }
}
