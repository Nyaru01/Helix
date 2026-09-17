using System.Windows;
using LocalBlast.Windows;
using LocalBlast.Services;
using Wpf.Ui.Appearance;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace LocalBlast;

public partial class App : System.Windows.Application
{
    private MainWindow? _mainWindow;
    private Forms.NotifyIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ApplicationThemeManager.ApplySystemTheme();
        _mainWindow = new MainWindow();
        ConfigureTrayIcon(_mainWindow);

        var splash = new SplashWindow();
        splash.Show();
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            splash.Close();
            _mainWindow.Show();
            _ = CheckForUpdateAsync(_mainWindow);
        };
        timer.Start();
    }

    private void ConfigureTrayIcon(MainWindow window)
    {
        _trayIcon = new Forms.NotifyIcon
        {
            Icon = Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? Drawing.SystemIcons.Application,
            Text = "Helix Blast",
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => RestoreWindow(window);
        window.StateChanged += (_, _) =>
        {
            if (window.WindowState == WindowState.Minimized)
                window.Hide();
        };
        window.Closing += (_, _) =>
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        };
    }

    private static void RestoreWindow(Window window)
    {
        window.Show();
        window.WindowState = WindowState.Normal;
        window.Activate();
    }

    private static async Task CheckForUpdateAsync(MainWindow window)
    {
        try
        {
            var update = await UpdateService.CheckAsync();
            if (update is null) return;
            var box = new Wpf.Ui.Controls.MessageBox
            {
                Owner = window,
                Title = "Update available",
                Content = $"Helix Blast {update.Version} is available. It can be installed automatically, then the application will restart.",
                PrimaryButtonText = "Install now",
                CloseButtonText = "Later"
            };
            if (await box.ShowDialogAsync() != Wpf.Ui.Controls.MessageBoxResult.Primary) return;

            window.IsEnabled = false;
            var installerPath = await UpdateService.DownloadAsync(update);
            UpdateService.LaunchInstaller(installerPath);
            Current.Shutdown();
        }
        catch (Exception ex)
        {
            window.IsEnabled = true;
            AppLogger.Error("Could not install the automatic update.", ex);
        }
    }
}
