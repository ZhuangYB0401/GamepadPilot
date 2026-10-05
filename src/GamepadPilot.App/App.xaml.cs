using System.Windows;
using System.Windows.Threading;
using GamepadPilot.App.Platform;

namespace GamepadPilot.App;

public partial class App : System.Windows.Application
{
    private SingleInstanceService? _singleInstance;
    private AppController? _controller;
    private MainWindow? _mainWindow;
    private bool _isExiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        if (e.Args.Any(x => string.Equals(x, "--preview-typing-wheel", StringComparison.OrdinalIgnoreCase)))
        {
            _controller = new AppController();
            _mainWindow = new MainWindow(_controller);
            MainWindow = _mainWindow;
            _controller.ExitRequested += (_, _) => ExitApplication();
            _mainWindow.Show();
            _controller.Initialize(_mainWindow);
            _mainWindow.Hide();
            _controller.ShowTypingWheelPreview();
            return;
        }

        _singleInstance = new SingleInstanceService();
        if (!_singleInstance.IsFirstInstance)
        {
            SingleInstanceService.ActivateExisting();
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        _controller = new AppController();
        _mainWindow = new MainWindow(_controller);
        MainWindow = _mainWindow;
        _controller.OpenRequested += (_, _) => Dispatcher.BeginInvoke(() => ShowMainWindow());
        _controller.ExitRequested += (_, _) => ExitApplication();
        _singleInstance.StartListener(() => Dispatcher.BeginInvoke(() => ShowMainWindow()));

        var startInTray = e.Args.Any(x => string.Equals(x, "--tray", StringComparison.OrdinalIgnoreCase));
        if (!startInTray)
        {
            _mainWindow.Show();
        }
        else
        {
            _mainWindow.Show();
            _mainWindow.Hide();
        }

        _controller.Initialize(_mainWindow);
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.ShowFromTray();
    }

    private void ExitApplication()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        if (_mainWindow is not null) _mainWindow.AllowClose = true;
        _controller?.Dispose();
        _controller = null;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"GamepadPilot 遇到错误：{e.Exception.Message}\n请查看日志目录获取详细信息。",
            "GamepadPilot",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
