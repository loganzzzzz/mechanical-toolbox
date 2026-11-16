using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MechToolbox.Core;

namespace MechToolbox.Desktop.Views;

public sealed class BoltViewModel : INotifyPropertyChanged
{
    public BoltModule Module { get; }
    public IReadOnlyList<BoltSize> Sizes => Module.Data.Sizes;
    public BoltReverseViewModel Reverse { get; }
    private BoltSize selectedSize;
    private ClearanceResult selectedClearance;
    private IReadOnlyList<ClearanceResult> clearances;
    public BoltViewModel(BoltModule module)
    {
        Module = module;
        selectedSize = Sizes.FirstOrDefault(x => x.Diameter == 8) ?? Sizes[0];
        clearances = Module.Clearances(selectedSize);
        selectedClearance = clearances[1];
        Reverse = new BoltReverseViewModel(module);
    }
    public BoltSize SelectedSize
    {
        get => selectedSize;
        set
        {
            if (value is null || value == selectedSize) return;
            var series = selectedClearance.Series;
            selectedSize = value;
            clearances = Module.Clearances(value);
            // Set the dependent selection before notifying the list to avoid stale values.
            selectedClearance = clearances.Single(x => x.Series == series);
            Notify(); Notify(nameof(Clearances)); Notify(nameof(SelectedClearance));
            Notify(nameof(SelectionTitle)); Notify(nameof(ToleranceText));
            Notify(nameof(SourceText)); Notify(nameof(Head)); Notify(nameof(HeadNotice));
            Notify(nameof(ThreadLimits));
        }
    }
    public IReadOnlyList<ClearanceResult> Clearances => clearances;
    public ClearanceResult SelectedClearance
    {
        get => selectedClearance;
        set { if (value is null) return; selectedClearance = value; Notify(); Notify(nameof(SelectionTitle)); Notify(nameof(ToleranceText)); }
    }
    public string SelectionTitle => $"{SelectedClearance.Name}  ·  {SelectedClearance.SizeText} mm";
    public string ToleranceText => $"参考孔偏差：{SelectedClearance.DeviationText}；{SelectedClearance.LimitsText}（另按图样确定）";
    public ThreadLimits? ThreadLimits => MetricThreadLimits.For(SelectedSize);
    public SocketHeadSize? Head => Module.HeadFor(SelectedSize);
    public string HeadNotice => Head is null ? $"GB/T 70.1-2008 未列 {SelectedSize.Spec}，不外推头部尺寸。" : Head.DetailText + (SelectedSize.Diameter == 14 ? " M14 为非优选规格。" : "");
    public string SourceText => $"通孔 {Module.Data.Sources.Single(x => x.Id == Module.Data.ClearanceSourceId).Title} · 底孔 {Module.TapSource(SelectedSize).Title} · 数据 {Module.Data.DataVersion}";
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class BoltQueryView : UserControl
{
    private readonly BoltViewModel vm;
    private readonly Action<string> status;
    public BoltQueryView(BoltModule module, Action<string> status)
    {
        vm = new BoltViewModel(module);
        this.status = status;
        InitializeComponent();
        DataContext = vm;
    }
    private void QuickSelect(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && decimal.TryParse(tag, NumberStyles.Number, CultureInfo.InvariantCulture, out var size))
            vm.SelectedSize = vm.Module.Find(size);
    }
    private void CopyResult(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(vm.Module.Summary(vm.SelectedSize, vm.SelectedClearance.Series));
            status($"已复制 {vm.SelectedSize.Spec} 查询结果");
        }
        catch (Exception ex) { MessageBox.Show($"复制失败，请稍后重试。\n{ex.Message}", "剪贴板暂不可用"); }
    }
    public void OpenMatch(BoltHoleMatch match)
    {
        vm.SelectedSize = match.Size;
        if (match.Clearance is { } series) vm.SelectedClearance = vm.Clearances.Single(x => x.Series == series);
        ((TabControl)Content).SelectedIndex = 0;
        status($"孔径反查：已打开 {match.Size.Spec} · {match.HoleName}");
    }
    private void ReverseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject origin || ItemsControl.ContainerFromElement(ReverseTable, origin) is not DataGridRow { Item: BoltHoleMatch match }) return;
        OpenMatch(match);
    }
}
