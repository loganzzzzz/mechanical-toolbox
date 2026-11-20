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
    private KeywaySize selectedSize;
    private KeywayFitOption selectedFit = KeywayModule.Fits[0];
    public KeywayResult? Result { get; private set; }
    public string Error { get; private set; } = "";
    public bool HasResult => Result is not null;
    public string SelectionText => Result is { } r ? $"轴径 {r.DiameterText} · {r.SelectionText}" : Error;
    public string SourceText => $"截面尺寸：{Module.Data.Standard} 表 1 · 数据 {Module.Data.DataVersion}";
    public string SupportedRange => $"收录 {Sizes.Count} 档国标键截面；轴径最多 3 位小数，不用于自动选键。";
    public KeywayViewModel(KeywayModule module) { Module = module; selectedSize = Sizes.Single(x => x.Width == 8); Recalculate(); }
    public string DiameterText
    {
        get => diameterText;
        set { diameterText = value ?? ""; Notify(); Recalculate(); }
    }
    public KeywaySize SelectedSize
    {
        get => selectedSize;
        set { if (value is null || !Sizes.Contains(value)) return; selectedSize = value; Notify(); Recalculate(); }
    }
    public KeywayFitOption SelectedFit
    {
        get => selectedFit;
        set { if (value is null || !Fits.Contains(value)) return; selectedFit = value; Notify(); Recalculate(); }
    }
    private void Recalculate()
    {
        Result = null;
        var text = diameterText.Trim();
        if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var diameter) || decimal.Round(diameter, 3) != diameter)
            Error = "请输入有效轴径，使用小数点，最多 3 位小数。";
        else
        {
            try { Result = Module.Query(diameter, selectedSize.Width, selectedFit.Fit); Error = ""; }
            catch (ArgumentOutOfRangeException) { Error = "轴径必须大于所选截面的轴槽深度，才能计算检验尺寸。"; }
        }
        Notify(nameof(Result)); Notify(nameof(Error)); Notify(nameof(HasResult)); Notify(nameof(SelectionText));
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
