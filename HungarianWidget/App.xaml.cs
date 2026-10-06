using System.Threading;
using System.Windows;
using HungarianWidget.Services;

namespace HungarianWidget;

public partial class App : System.Windows.Application
{
    private static readonly Mutex SingleInstanceMutex = new(false, "Local\\MagyarWidget-Desktop-Learner");

    public ContentCatalog Catalog { get; private set; } = null!;
    public ProgressStore Progress { get; private set; } = null!;
    public bool IsFirstInstance { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        IsFirstInstance = SingleInstanceMutex.WaitOne(TimeSpan.Zero, true);
        if (!IsFirstInstance)
        {
            Shutdown();
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            Catalog = ContentCatalog.Load();
            Progress = new ProgressStore();
            var window = new MainWindow(Catalog, Progress);
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Magyar could not start.\n\n{ex.Message}", "Magyar", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (IsFirstInstance)
        {
            try { SingleInstanceMutex.ReleaseMutex(); }
            catch (ApplicationException) { }
        }
        SingleInstanceMutex.Dispose();
        base.OnExit(e);
    }
}
