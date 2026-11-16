using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace MechToolbox.Core;

public enum BoltHoleKind { Fine, Medium, Coarse, TapDrill }
public sealed record BoltHoleMatch(BoltSize Size, BoltHoleKind Kind, string HoleName,
    decimal NominalDiameter, decimal Difference)
{
    public string Spec => Size.Spec;
    public string HoleText => $"⌀{KeyFormat.Number(NominalDiameter)}";
    public string DifferenceText => Difference > 0 ? $"+{KeyFormat.Number(Difference)}" : KeyFormat.Number(Difference);
    public ClearanceSeries? Clearance => Kind switch
    {
        BoltHoleKind.Fine => ClearanceSeries.Fine,
        BoltHoleKind.Medium => ClearanceSeries.Medium,
        BoltHoleKind.Coarse => ClearanceSeries.Coarse,
        _ => null
    };
}

public static class BoltReverse
{
    public const decimal SearchRadius = 3m;
    public static IReadOnlyList<BoltHoleMatch> Find(BoltModule module, decimal diameter)
    {
        if (diameter <= 0 || diameter > 1000) throw new ArgumentOutOfRangeException(nameof(diameter));
        var matches = new List<BoltHoleMatch>();
        foreach (var size in module.Data.Sizes)
        {
            (BoltHoleKind Kind, string Name, decimal Diameter)[] candidates = [
                (BoltHoleKind.Fine, "通孔 · 精装配", size.Fine),
                (BoltHoleKind.Medium, "通孔 · 中等装配", size.Medium),
                (BoltHoleKind.Coarse, "通孔 · 粗装配", size.Coarse),
                (BoltHoleKind.TapDrill, "攻丝前底孔 · 切削", size.TapDrill)
            ];
            foreach (var c in candidates)
            {
                var difference = c.Diameter - diameter;
                if (Math.Abs(difference) <= SearchRadius)
                    matches.Add(new(size, c.Kind, c.Name, c.Diameter, difference));
            }
        }
        return matches.OrderBy(x => Math.Abs(x.Difference)).ThenBy(x => x.Size.Diameter).ThenBy(x => x.Kind).ToArray();
    }
}

public sealed class BoltReverseViewModel : INotifyPropertyChanged
{
    public BoltModule Module { get; }
    private string diameterText = "12";
    public IReadOnlyList<BoltHoleMatch> Matches { get; private set; } = [];
    public string Message { get; private set; } = "";
    public BoltReverseViewModel(BoltModule module) { Module = module; Recalculate(); }
    public string DiameterText
    {
        get => diameterText;
        set { diameterText = value ?? ""; Notify(); Recalculate(); }
    }
    private static bool Parse(string text, out decimal value) => decimal.TryParse(text.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value) && decimal.Round(value, 3) == value;
    private void Recalculate()
    {
        Matches = [];
        if (!Parse(diameterText, out var diameter) || diameter <= 0 || diameter > 1000)
            Message = "请输入大于 0、且不超过 1000 mm 的孔径，最多 3 位小数。";
        else
        {
            Matches = BoltReverse.Find(Module, diameter);
            var range = $"范围 {KeyFormat.Number(Math.Max(0, diameter - BoltReverse.SearchRadius))}～{KeyFormat.Number(diameter + BoltReverse.SearchRadius)} mm";
            Message = Matches.Count == 0 ? $"{range} 内没有匹配的通孔或攻丝底孔。" :
                $"{range}：{Matches.Select(x => x.Size.Spec).Distinct().Count()} 个粗牙标号，共 {Matches.Count} 项。按孔径差值从小到大排列，双击查看尺寸。";
        }
        Notify(nameof(Matches)); Notify(nameof(Message));
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
