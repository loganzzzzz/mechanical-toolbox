using System.Windows;
using System.Windows.Controls;
using MechToolbox.Core;
namespace MechToolbox.Desktop.Views;
public partial class CirclipQueryView : UserControl
{
    private readonly CirclipViewModel vm;
    private readonly Action<string> status;
    public CirclipQueryView(CirclipModule module, Action<string> status)
    { vm = new(module); this.status = status; InitializeComponent(); DataContext = vm; }
    private void CopyResult(object sender, RoutedEventArgs e)
    {
        if (vm.Result is not { } result) return;
        try { Clipboard.SetText(vm.Module.Summary(result)); status($"已复制 {result.Designation}"); }
        catch (Exception ex) { MessageBox.Show($"复制失败，请稍后重试。\n{ex.Message}","剪贴板暂不可用"); }
    }
}
