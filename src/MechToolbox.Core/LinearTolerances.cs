namespace MechToolbox.Core;

/// <summary>ISO 286-2:2010 table 1. Basic size ranges are open below, closed above; values are mm.</summary>
public static class LinearTolerances
{
    public static StandardSource Source { get; } = new("iso286", "ISO 286-2:2010 · 表 1",
        "https://www.iso.org/standard/54915.html", "H12、H13、H14 数值偏差按实际孔径或槽宽分档，H 下偏差为 0；本版数值表适用大于 0 至 50 mm。既适用于孔径，也适用于槽的两平行表面间距。");
    private static readonly (decimal Max, decimal It12, decimal It13, decimal It14)[] Bands =
    [ (3,.1m,.14m,.25m), (6,.12m,.18m,.3m), (10,.15m,.22m,.36m),
      (18,.18m,.27m,.43m), (30,.21m,.33m,.52m), (50,.25m,.39m,.62m) ];
    public static SlotTolerance Hole(decimal basicSize, string band)
    {
        if (basicSize <= 0 || basicSize > Bands[^1].Max) throw new ArgumentOutOfRangeException(nameof(basicSize));
        var row = Bands.First(x => basicSize <= x.Max);
        return new(0, band switch { "H12" => row.It12, "H13" => row.It13, "H14" => row.It14,
            _ => throw new ArgumentOutOfRangeException(nameof(band), "未支持的线性公差带。") });
    }
}
