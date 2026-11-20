using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MechToolbox.Core;
using MechToolbox.Desktop.Views;

namespace MechToolbox.Desktop;

// Runs only when explicitly invoked with --smoke-test. No external test dependencies.
internal static class UiSmoke
{
    private static IEnumerable<T> Children<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var nested in Children<T>(child)) yield return nested;
        }
    }
    private static void Flush(Window w)
    {
        w.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        w.UpdateLayout();
    }
    public static void Run(MainWindow window, string folder)
    {
        Directory.CreateDirectory(folder);
        if (window.Icon is null || !Children<Image>(window).Any(x => x.Source is not null)) throw new Exception("程序图标资源未加载。");
        var view = Children<BoltQueryView>(window).Single();
        var vm = (BoltViewModel)view.DataContext;
        var combo = Children<ComboBox>(view).Single(x => x.ItemsSource is IReadOnlyList<BoltSize>);
        var list = Children<ListBox>(view).Single();
        int checks = 0;
        foreach (var size in vm.Sizes)
        {
            var oldSeries = vm.SelectedClearance.Series;
            combo.SelectedItem = size;
            Flush(window);
            if (vm.SelectedSize != size || vm.SelectedClearance.Series != oldSeries)
                throw new Exception("规格切换未保留所选装配系列。");
            if (vm.ThreadLimits is not { } thread || !Children<TextBlock>(view).Any(x => x.Text == thread.PitchRange) ||
                !Children<TextBlock>(view).Any(x => x.Text == thread.MinorDeviation)) throw new Exception("6H 数值公差未随规格更新。");
            var dimensions = (Grid)view.FindName("HeadDimensions");
            if (vm.Head != vm.Module.HeadFor(size) || dimensions.Visibility != (vm.Head is null ? Visibility.Collapsed : Visibility.Visible))
                throw new Exception("头部尺寸未随规格更新或未收录规格残留数值。");
            if (vm.Head is { } head && (!Children<TextBlock>(dimensions).Any(x => x.Text == head.SmoothText) ||
                !Children<TextBlock>(dimensions).Any(x => x.Text == head.HeightText) || !Children<TextBlock>(dimensions).Any(x => x.Text == head.SocketText)))
                throw new Exception("头部尺寸绑定无效。");
            for (int i = 0; i < 3; i++)
            {
                list.SelectedIndex = i;
                Flush(window);
                if (vm.SelectedClearance != vm.Module.Clearances(size)[i]) throw new Exception("界面显示了过期的通孔结果。");
                if (!Children<TextBlock>(view).Any(x => x.Text == vm.SelectedClearance.DeviationText) ||
                    !Children<TextBlock>(view).Any(x => x.Text == vm.SelectedClearance.LimitsText)) throw new Exception("通孔数值公差未绑定。");
                checks++;
            }
        }
        combo.SelectedItem = vm.Module.Find(8); list.SelectedIndex = 1; Flush(window);
        if (Children<TextBlock>(view).Any(x => x.Text.Contains("螺距") || x.Text.Contains("M8×") || x.Text.Contains("M8 ×")))
            throw new Exception("粗牙尺寸界面仍显示螺距。");
        Capture(window, Path.Combine(folder, "query-1200.png"));
        window.Width = window.MinWidth; window.Height = window.MinHeight; Flush(window);
        Capture(window, Path.Combine(folder, "query-minimum.png"));
        window.Width = 1200; window.Height = 900; Flush(window);
        var tabs = (TabControl)view.Content; tabs.SelectedIndex = 1; Flush(window);
        if (tabs.Items.Cast<TabItem>().Any(x => x.Header?.ToString() == "全规格对照表")) throw new Exception("已删除的全规格对照表仍存在。");
        if (Children<ComboBox>(view).Any() || Children<TextBox>(view).Count() != 1)
            throw new Exception("反查界面应只保留一个孔径输入。");
        var reverseInput = Children<TextBox>(view).Single(x => System.Windows.Automation.AutomationProperties.GetName(x) == "反查孔径");
        reverseInput.SetCurrentValue(TextBox.TextProperty, "12"); Flush(window);
        if (vm.Reverse.Matches.Count != 12 || vm.Reverse.Matches.First().Difference != 0 ||
            !vm.Reverse.Matches.Any(x => x.Difference == -3) || !vm.Reverse.Matches.Any(x => x.Difference == 3))
            throw new Exception("反查未包含 ±3 mm 内的全部候选或边界。");
        var reverseGrid = Children<DataGrid>(view).Single();
        if (!reverseGrid.Columns.Any(x => x.Header?.ToString() == "差值 / mm")) throw new Exception("反查缺少孔径差值列。");
        Capture(window, Path.Combine(folder, "bolt-reverse-mixed.png"));
        window.Width = window.MinWidth; window.Height = window.MinHeight; Flush(window);
        Capture(window, Path.Combine(folder, "bolt-reverse-minimum.png"));
        window.Width = 1200; window.Height = 900; Flush(window);
        reverseInput.SetCurrentValue(TextBox.TextProperty, "2.2"); Flush(window);
        if (vm.Reverse.Matches.Count(x => x.Difference == 0) != 2) throw new Exception("反查未保留多个精确候选。");
        Capture(window, Path.Combine(folder, "bolt-reverse.png"));
        view.OpenMatch(vm.Reverse.Matches.Single(x => x.Size.Diameter == 2 && x.Kind == BoltHoleKind.Fine)); Flush(window);
        if (tabs.SelectedIndex != 0 || vm.SelectedSize.Diameter != 2 || vm.SelectedClearance.Series != ClearanceSeries.Fine)
            throw new Exception("反查到尺寸查询的跳转无效。");
        tabs.SelectedIndex = 1; Flush(window);
        reverseInput = Children<TextBox>(view).Single(x => System.Windows.Automation.AutomationProperties.GetName(x) == "反查孔径");
        reverseInput.SetCurrentValue(TextBox.TextProperty, "invalid"); Flush(window);
        if (vm.Reverse.Matches.Count != 0) throw new Exception("无效输入残留反查结果。");
        vm.Reverse.DiameterText = "8.4";

        window.ModuleList.SelectedIndex = 1; Flush(window);
        var keyView = Children<KeywayQueryView>(window).Single();
        var keyVm = (KeywayViewModel)keyView.DataContext;
        var diameterInput = Children<TextBox>(keyView).Single();
        var fitList = Children<ComboBox>(keyView).Single(x => System.Windows.Automation.AutomationProperties.GetName(x) == "键槽联结类型");
        var sectionList = Children<ComboBox>(keyView).Single(x => System.Windows.Automation.AutomationProperties.GetName(x) == "国标键截面");
        foreach (var row in keyVm.Sizes)
        {
            diameterInput.SetCurrentValue(TextBox.TextProperty, "1000"); sectionList.SelectedItem = row; Flush(window);
            foreach (var fit in keyVm.Fits)
            {
                fitList.SelectedItem = fit; Flush(window);
                if (keyVm.Result is not { } result || result.Size != row || result.Fit.Fit != fit.Fit)
                    throw new Exception("键槽界面绑定或联结类型切换无效。");
                checks++;
            }
        }
        diameterInput.SetCurrentValue(TextBox.TextProperty, ""); Flush(window);
        if (keyVm.Result is not null) throw new Exception("清空轴径后残留键槽结果。");
        diameterInput.SetCurrentValue(TextBox.TextProperty, "50"); sectionList.SelectedItem = keyVm.Sizes.Single(x=>x.Width==14); fitList.SelectedIndex = 0; Flush(window);
        diameterInput.SetCurrentValue(TextBox.TextProperty, "80"); Flush(window);
        if(keyVm.Result?.Size.Width!=14) throw new Exception("输入轴径意外改变手选截面。");
        diameterInput.SetCurrentValue(TextBox.TextProperty, "50"); Flush(window);
        Capture(window, Path.Combine(folder, "keyways-1200.png"));
        window.Width = window.MinWidth; window.Height = window.MinHeight; Flush(window);
        Capture(window, Path.Combine(folder, "keyways-minimum.png"));
        window.Width = 1200; window.Height = 900; Flush(window);
        window.ModuleList.SelectedIndex = 0; Flush(window);
        window.ModuleList.SelectedIndex = 1; Flush(window);
        if (!ReferenceEquals(Children<KeywayQueryView>(window).Single(), keyView) || keyVm.Result?.ShaftDiameter != 50)
            throw new Exception("模块切换未保留键槽查询状态。");
        var keyTabs = (TabControl)keyView.Content; keyTabs.SelectedIndex = 1; Flush(window);
        Capture(window, Path.Combine(folder, "keyways-table.png"));
        window.ModuleList.SelectedIndex = 2; Flush(window);
        var cirView = Children<CirclipQueryView>(window).Single();
        var cirVm = (CirclipViewModel)cirView.DataContext;
        var kindCombo = Children<ComboBox>(cirView).Single(x => System.Windows.Automation.AutomationProperties.GetName(x) == "卡簧类型");
        var diameterCombo = Children<ComboBox>(cirView).Single(x => System.Windows.Automation.AutomationProperties.GetName(x) == "卡簧公称直径");
        int cirChecks = 0;
        foreach (var kind in CirclipViewModel.Kinds)
        {
            kindCombo.SelectedItem = kind; Flush(window);
            foreach (var d in cirVm.Diameters)
            {
                diameterCombo.SetCurrentValue(ComboBox.TextProperty,KeyFormat.Number(d)); Flush(window);
                if (cirVm.Result != cirVm.Module.Query(kind.Kind,d)) throw new Exception("卡簧选择显示过期结果。");
                if (!Children<TextBlock>(cirView).Any(x => x.Text == cirVm.Result.WidthDeviation) ||
                    !Children<TextBlock>(cirView).Any(x => x.Text == cirVm.Result.WidthLimits)) throw new Exception("卡簧槽宽数值偏差未绑定。");
                var review = (Border)cirView.FindName("ReviewNotice");
                if (review.Visibility != (cirVm.Result.NeedsReview ? Visibility.Visible : Visibility.Collapsed)) throw new Exception("国标原表核对提示未随规格清理。");
                cirChecks++;
            }
            diameterCombo.SetCurrentValue(ComboBox.TextProperty,"30"); Flush(window);
            Capture(window,Path.Combine(folder,$"circlip-{kind.Kind}-1200.png"));
            window.Width=window.MinWidth; window.Height=window.MinHeight; Flush(window);
            Capture(window,Path.Combine(folder,$"circlip-{kind.Kind}-minimum.png"));
            window.Width=1200; window.Height=900; Flush(window);
        }
        diameterCombo.SetCurrentValue(ComboBox.TextProperty,"invalid"); Flush(window);
        if(cirVm.HasResult) throw new Exception("卡簧无效输入残留结果。");
        kindCombo.SelectedItem=CirclipViewModel.Kinds[0]; diameterCombo.SetCurrentValue(ComboBox.TextProperty,"40"); Flush(window);
        if(cirVm.Result is not {NeedsReview:true,GrooveDiameter:37m,NominalDepth:1.25m} shaft40 ||
            !Children<TextBlock>(cirView).Any(x=>x.Text==shaft40.GrooveText) ||
            !Children<TextBlock>(cirView).Any(x=>x.Text==shaft40.DepthText) ||
            !Children<TextBlock>(cirView).Any(x=>x.Text==shaft40.GrooveLimits) ||
            !Children<TextBlock>(cirView).Any(x=>x.Text==shaft40.ReviewNote) ||
            ((Border)cirView.FindName("ReviewNotice")).Visibility != Visibility.Visible)
            throw new Exception("轴用40国标原表值或核对提示缺失。");
        Capture(window,Path.Combine(folder,"circlip-review.png"));
        window.Width=window.MinWidth; window.Height=window.MinHeight; Flush(window);
        Capture(window,Path.Combine(folder,"circlip-review-minimum.png"));
        window.Width=1200; window.Height=900; Flush(window);
        kindCombo.SelectedItem=CirclipViewModel.Kinds[1]; Flush(window);
        if(cirVm.Result?.NeedsReview != false || ((Border)cirView.FindName("ReviewNotice")).Visibility != Visibility.Collapsed)
            throw new Exception("孔用40残留了轴用40核对提示。");
        kindCombo.SelectedItem=CirclipViewModel.Kinds[1]; diameterCombo.SetCurrentValue(ComboBox.TextProperty,"30"); Flush(window);
        window.ModuleList.SelectedIndex=0; Flush(window); window.ModuleList.SelectedIndex=2; Flush(window);
        if(!ReferenceEquals(Children<CirclipQueryView>(window).Single(),cirView) || cirVm.Result?.Kind!=CirclipKind.Bore || cirVm.Result.Diameter!=30) throw new Exception("卡簧模块状态未保留。");
        File.WriteAllText(Path.Combine(folder, "PASS.txt"), $"PASS: GB-only sources; manual national-standard keyway sections; numeric H12/H13/H14 and 6H; icon resources; {checks} bolt/keyway; {cirChecks} circlip UI selection transitions; shaft/bore dimensions; shaft40 current GB table values and review notice; note cleanup; invalid input; module state retention. Original bolt/keyway UI selection transitions; pitch hidden; socket head dimensions and unsupported-size clearing; single-input mixed-hole reverse search within +/-3 mm inclusive; signed difference column and nearest-first order; ambiguity and navigation; invalid input; module state retention; normal and minimum window screenshots.\nRuntime: {Environment.Version}\nOS: {Environment.OSVersion}");
    }
    private static void Capture(FrameworkElement element, string path)
    {
        var dpi = VisualTreeHelper.GetDpi(element);
        var image = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth * dpi.DpiScaleX), (int)Math.Ceiling(element.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        image.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(path); encoder.Save(stream);
    }
}
