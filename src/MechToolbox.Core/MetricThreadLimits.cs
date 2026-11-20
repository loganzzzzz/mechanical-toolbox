namespace MechToolbox.Core;

public sealed record ThreadLimits(decimal Diameter, decimal Pitch, decimal PitchMin, decimal PitchMax,
    decimal MinorMin, decimal MinorMax)
{
    public string PitchDeviation => $"中径 D₂ 上 / 下偏差：{KeyFormat.Deviation(PitchMax-PitchMin, 0)} mm";
    public string MinorDeviation => $"小径 D₁ 上 / 下偏差：{KeyFormat.Deviation(MinorMax-MinorMin, 0)} mm";
    public string PitchRange => $"中径 D₂：{KeyFormat.Number(PitchMin)} ～ {KeyFormat.Number(PitchMax)} mm";
    public string MinorRange => $"小径 D₁：{KeyFormat.Number(MinorMin)} ～ {KeyFormat.Number(MinorMax)} mm";
    public string Summary => $"6H 成品内螺纹\n{PitchDeviation}\n{PitchRange}\n{MinorDeviation}\n{MinorRange}";
}

public static class MetricThreadLimits
{
    public static StandardSource Source { get; } = new("gb15756", "GB/T 15756-2008 · 普通螺纹 极限尺寸",
        "https://openstd.samr.gov.cn/bzgk/std/newGbInfo?hcno=1F4966A89595E56DFDCDD3FE1B0A8E22", "2026-11-20 官方题录确认为现行；表 3 正文第 19～22 页，23 档粗牙 6H 成品内螺纹中径与小径极限均按原表复核。H 下偏差为 0，上偏差为最大值减基本尺寸；不将攻丝前钻径套用成品螺纹公差。");
    private static readonly ThreadLimits[] Rows =
    [
        new(1.6m,.35m,1.373m,1.458m,1.221m,1.321m),
        new(1.8m,.35m,1.573m,1.658m,1.421m,1.521m),
        new(2,.4m,1.740m,1.830m,1.567m,1.679m),
        new(2.5m,.45m,2.208m,2.303m,2.013m,2.138m),
        new(3,.5m,2.675m,2.775m,2.459m,2.599m),
        new(3.5m,.6m,3.110m,3.222m,2.850m,3.010m),
        new(4,.7m,3.545m,3.663m,3.242m,3.422m),
        new(5,.8m,4.480m,4.605m,4.134m,4.334m),
        new(6,1,5.350m,5.500m,4.917m,5.153m),
        new(7,1,6.350m,6.500m,5.917m,6.153m),
        new(8,1.25m,7.188m,7.348m,6.647m,6.912m),
        new(10,1.5m,9.026m,9.206m,8.376m,8.676m),
        new(12,1.75m,10.863m,11.063m,10.106m,10.441m),
        new(14,2,12.701m,12.913m,11.835m,12.210m),
        new(16,2,14.701m,14.913m,13.835m,14.210m),
        new(18,2.5m,16.376m,16.600m,15.294m,15.744m),
        new(20,2.5m,18.376m,18.600m,17.294m,17.744m),
        new(22,2.5m,20.376m,20.600m,19.294m,19.744m),
        new(24,3,22.051m,22.316m,20.752m,21.252m),
        new(27,3,25.051m,25.316m,23.752m,24.252m),
        new(30,3.5m,27.727m,28.007m,26.211m,26.771m),
        new(33,3.5m,30.727m,31.007m,29.211m,29.771m),
        new(36,4,33.402m,33.702m,31.670m,32.270m)
    ];
    public static ThreadLimits? For(BoltSize size) => Rows.SingleOrDefault(x => x.Diameter == size.Diameter && x.Pitch == size.Pitch);
}
