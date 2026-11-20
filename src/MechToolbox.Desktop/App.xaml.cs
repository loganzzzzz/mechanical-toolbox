using System.IO;
using System.Windows;
using MechToolbox.Core;

namespace MechToolbox.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var data = new JsonBoltDataProvider().Load(Path.Combine(AppContext.BaseDirectory, "data", "metric-coarse.v1.json"));
            var registry = new ModuleRegistry();
            var heads = new JsonSocketHeadDataProvider().Load(Path.Combine(AppContext.BaseDirectory, "data", "socket-head-gbt70-1.v1.json"));
            registry.Register(new BoltModule(data, heads));
            var keyways = new JsonKeywayDataProvider().Load(Path.Combine(AppContext.BaseDirectory, "data", "parallel-keyways-gb.v2.json"));
            registry.Register(new KeywayModule(keyways));
            var circlips = new JsonCirclipDataProvider().Load(Path.Combine(AppContext.BaseDirectory, "data", "circlips-gb.v1.json"));
            registry.Register(new CirclipModule(circlips));
            MainWindow = new MainWindow(registry);
            if (e.Args.Length > 0 && e.Args[0] == "--smoke-test")
            {
                var folder = e.Args.Length > 1 ? e.Args[1] : Path.Combine(AppContext.BaseDirectory, "smoke-results");
                EventHandler? handler = null;
                handler = (_, _) =>
                {
                    MainWindow.ContentRendered -= handler;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try { UiSmoke.Run((MainWindow)MainWindow, folder); Shutdown(0); }
                        catch (Exception ex)
                        {
                            Directory.CreateDirectory(folder);
                            File.WriteAllText(Path.Combine(folder, "FAILED.txt"), ex.ToString());
                            Shutdown(1);
                        }
                    }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                };
                MainWindow.ContentRendered += handler;
            }
            MainWindow.Show();
        }
        catch (Exception ex)
        {
            if (e.Args.Length > 0 && e.Args[0] == "--smoke-test")
            {
                var folder = e.Args.Length > 1 ? e.Args[1] : Path.Combine(AppContext.BaseDirectory, "smoke-results");
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "FAILED-startup.txt"), ex.ToString());
                Shutdown(1);
                return;
            }
            MessageBox.Show($"无法加载标准数据，请保留完整的 data 文件夹。\n\n{ex.Message}", "机设工具箱 · 启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
