using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MechToolbox.Core;

namespace MechToolbox.Desktop.Views;

public partial class KeywayQueryView : UserControl
{
    private readonly KeywayViewModel vm;
    private readonly Action<string> status;
    public KeywayQueryView(KeywayModule module, Action<string> status)
    {
        vm = new KeywayViewModel(module); this.status = status;
        InitializeComponent(); DataContext = vm;
    }
    private void CopyResult(object sender, RoutedEventArgs e)
    {
        if (vm.Result is not { } result) return;
        try { Clipboard.SetText(vm.Module.Summary(result)); status($"已复制 {result.DiameterText} 键槽查询结果"); }
        catch (Exception ex) { MessageBox.Show($"复制失败，请稍后重试。\n{ex.Message}", "剪贴板暂不可用"); }
    }
    private void TableDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject origin || ItemsControl.ContainerFromElement(SizeTable, origin) is not DataGridRow { Item: KeywaySize size }) return;
        vm.SelectedSize = size;
        ((TabControl)Content).SelectedIndex = 0;
    }
}
