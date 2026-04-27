using System.Diagnostics;
using System.IO;
using System.Windows;

namespace GapLogger;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closing += async (_, e) =>
        {
            if (DataContext is ViewModels.MainViewModel vm && vm.IsRunning)
            {
                e.Cancel = true;
                if (vm.StopCommand.CanExecute(null)) vm.StopCommand.Execute(null);
                await Task.Delay(500);
                Application.Current.Shutdown();
            }
        };
    }

    private void OnOpenFolder(object sender, RoutedEventArgs e)
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var folder = Path.Combine(desktop, "check-gap");
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
    }
}