using System.IO;
using System.Windows;
using DymndAssetBrowser.Core.Persistence;

namespace DymndAssetBrowser.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private FileStream? _instanceLock;
    protected override void OnStartup(StartupEventArgs e)
    {
        var state = new ApplicationStateStore();
        try
        {
            Directory.CreateDirectory(state.AppDataRoot);
            _instanceLock = new FileStream(Path.Combine(state.AppDataRoot, "instance.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException)
        {
            MessageBox.Show("DYM&D Asset Browser is already open, or its data folder is unavailable. Close the existing window before opening another instance.", "DYM&D Asset Browser", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(1); return;
        }
        catch (UnauthorizedAccessException ex)
        {
            MessageBox.Show("Cannot access the application data folder.\n\n" + ex.Message, "DYM&D Asset Browser", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1); return;
        }
        base.OnStartup(e);
        MainWindow = new MainWindow(); MainWindow.Show();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        _instanceLock?.Dispose();
        base.OnExit(e);
    }
}
