using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MechToolbox.Core;
using MechToolbox.Desktop.Views;

namespace MechToolbox.Desktop;

// WPF presentation adapters are registered separately from domain modules.
public sealed class DesktopModuleRegistry
{
    private readonly Dictionary<string, Func<IStandardModule, Action<string>, FrameworkElement>> factories = [];
    public void Register(string id, Func<IStandardModule, Action<string>, FrameworkElement> factory) => factories.Add(id, factory);
    public FrameworkElement Create(IStandardModule module, Action<string> status) =>
        factories.TryGetValue(module.Descriptor.Id, out var factory) ? factory(module, status)
        : throw new InvalidOperationException($"模块 {module.Descriptor.Id} 尚未注册桌面界面。");
}

public partial class MainWindow : Window
{
    private readonly ModuleRegistry registry;
    private readonly DesktopModuleRegistry desktop = new();
    private readonly Dictionary<string, FrameworkElement> pages = [];
    public MainWindow(ModuleRegistry registry)
    {
        this.registry = registry;
        desktop.Register("bolt-holes", (module, status) => new BoltQueryView((BoltModule)module, status));
        desktop.Register("keyways", (module, status) => new KeywayQueryView((KeywayModule)module, status));
        desktop.Register("circlips", (module, status) => new CirclipQueryView((CirclipModule)module, status));
        InitializeComponent();
        ModuleList.ItemsSource = registry.Modules.Select(x => x.Descriptor);
        ModuleList.SelectedIndex = 0;
    }
    private void ModuleChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModuleList.SelectedItem is not ModuleDescriptor descriptor) return;
        var module = registry.Modules.Single(x => x.Descriptor.Id == descriptor.Id);
        if (!pages.TryGetValue(descriptor.Id, out var page))
            pages[descriptor.Id] = page = desktop.Create(module, text => Status.Text = text);
        PageTitle.Text = descriptor.Name;
        StandardBadge.Text = descriptor.Id switch { "keyways" => "普通平键  /  GB/T", "circlips" => "标准型 A 型  /  GB/T", _ => "公制粗牙  /  ISO · GB/T" };
        Status.Text = "就绪 · 所有尺寸单位为 mm";
        PageHost.Content = page;
    }
    private StackPanel NewInfoPage(string title)
    {
        ModuleList.SelectedIndex = -1;
        PageTitle.Text = title;
        StandardBadge.Text = "标准库  /  mm";
        var panel = new StackPanel { Margin = new Thickness(10) };
        PageHost.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        return panel;
    }
    private static void Paragraph(Panel panel, string title, string text)
    {
        panel.Children.Add(new TextBlock { Text = title, FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 10) });
        panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 14, LineHeight = 25, Margin = new Thickness(0, 0, 0, 20) });
    }
    private void ShowSources(object sender, RoutedEventArgs e)
    {
        var panel = NewInfoPage("标准数据与依据");
        foreach (var module in registry.Modules.OfType<IStandardDataModule>())
        {
            Paragraph(panel, module.Descriptor.Name, module.DataSummary);
            foreach (var source in module.Sources)
            {
                Paragraph(panel, source.Title, source.Scope);
                var button = new Button { Content = "查看原始来源 ↗", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 20) };
                button.Click += (_, _) =>
                {
                    try { Process.Start(new ProcessStartInfo(source.Url) { UseShellExecute = true }); }
                    catch (Exception ex) { MessageBox.Show($"无法打开浏览器：{ex.Message}", "查看来源"); }
                };
                panel.Children.Add(button);
            }
        }
        Paragraph(panel, "如何理解结果", "内螺纹的 6H 是一般用途建议公差带，特殊配合应按设计确定。底孔是切削攻丝的常用钻孔推荐值，需结合材料、刀具和工艺确认；不用于挤压丝锥。\n精、中、粗装配表示通孔间隙系列，不能单独保证定位精度。ISO 273 将 H12 / H13 / H14 列为可参考公差带，本软件不计算极限偏差。\n盲孔的有效牙深、钻尖余量、丝锥引导长度，以及连接强度需要另行设计，当前版本不推算孔深。");
        Paragraph(panel, "键槽尺寸说明", "普通平键的截面尺寸、槽宽公差、槽深和圆角按 GB/T 1095-2003 表 1 查询。正常联结为轴 N9 / 毂 JS9，紧密联结为轴与毂 P9，松联结为轴 H9 / 毂 D10。\n轴径分档沿用设计手册的常用推荐，GB/T 1095-2003 原表不规定轴径。键长、连接强度及形位公差应按具体结构另行设计。\n检验尺寸 d−t₁ 的偏差与 t₁ 反向；d+t₂ 与 t₂ 同向。本页不计入轴径自身的制造偏差。");
    }
    private void ShowExtensions(object sender, RoutedEventArgs e)
    {
        var panel = NewInfoPage("扩展与版本");
        Paragraph(panel, "机设工具箱  v0.3.1", "原生 Windows 桌面应用 · 完全离线查询 · 无需管理员权限\n建议运行环境：Windows 10 / 11，64 位。\n粗牙标号省略螺距；孔径反查只需一个输入，同时列出 ±3 mm 内的通孔与攻丝底孔，含边界并显示差值。尺寸查询增加 GB/T 70.1-2008 内六角圆柱头的头径、头高和内六角尺寸。新增轴用与孔用卡簧及卡簧槽查询（标准型 A 型）。");
        Paragraph(panel, "已预留的扩展接口", "① 功能模块 IStandardModule：为新标准定义标识、名称和查询逻辑。\n② 数据提供器 IStandardDataProvider<T>：独立读取 JSON、数据库或企业标准。\n③ 界面适配 DesktopModuleRegistry：注册新模块后自动进入侧边栏。\n④ 数据版本 SchemaVersion / DataVersion：启动时校验结构、尺寸和来源。");
        Paragraph(panel, "下一阶段可以接入", "沉孔与沉头孔、销孔、轴孔配合、常用型材等。新功能通过独立模块和独立数据集接入，当前版本尚未提供这些查询。");
        Paragraph(panel, "数据维护", "螺纹数据位于 data/metric-coarse.v1.json；键槽数据位于 data/parallel-keyways-gb.v1.json；内六角圆柱头数据位于 data/socket-head-gbt70-1.v1.json；卡簧数据位于 data/circlips-gb.v1.json。更新数据须核对来源并同步版本和回归测试；缺失或无效的数据会阻止启动。详细开发接口见源码中的 docs/EXTENDING.md。");
    }
}
