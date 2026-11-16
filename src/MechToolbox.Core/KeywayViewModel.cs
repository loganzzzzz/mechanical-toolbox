using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace MechToolbox.Core;

// Independent of WPF so invalid-input/stale-result behavior can be tested on any OS.
public sealed class KeywayViewModel : INotifyPropertyChanged
{
    public KeywayModule Module { get; }
    public IReadOnlyList<KeywaySize> Sizes => Module.Data.Sizes;
    public IReadOnlyList<KeywayFitOption> Fits => KeywayModule.Fits;
    private string diameterText = "30";
    private KeywayFitOption selectedFit = KeywayModule.Fits[0];
    public KeywayResult? Result { get; private set; }
    public string Error { get; private set; } = "";
    public bool HasResult => Result is not null;
    public string SelectionText => Result is { } r ? $"轴径 {r.DiameterText} · {r.RangeText}" : Error;
    public string SourceText => $"截面尺寸：{Module.Data.Standard} 表 1 · 数据 {Module.Data.DataVersion}";
    public string SupportedRange => $"收录推荐轴径：6～{KeyFormat.Number(Sizes.Last().ShaftMax)} mm，最多 3 位小数。";
    public KeywayViewModel(KeywayModule module) { Module = module; Recalculate(); }
    public string DiameterText
    {
        get => diameterText;
        set { diameterText = value ?? ""; Notify(); Recalculate(); }
    }
    public KeywayFitOption SelectedFit
    {
        get => selectedFit;
        set { if (value is null) return; selectedFit = value; Notify(); Recalculate(); }
    }
    private void Recalculate()
    {
        Result = null;
        var text = diameterText.Trim();
        if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var diameter) || decimal.Round(diameter, 3) != diameter)
            Error = "请输入有效轴径，使用小数点，最多 3 位小数。";
        else
        {
            try { Result = Module.Query(diameter, selectedFit.Fit); Error = ""; }
            catch (ArgumentOutOfRangeException) { Error = $"请输入 6～{KeyFormat.Number(Sizes.Last().ShaftMax)} mm 范围内的轴径。"; }
        }
        Notify(nameof(Result)); Notify(nameof(Error)); Notify(nameof(HasResult)); Notify(nameof(SelectionText));
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
