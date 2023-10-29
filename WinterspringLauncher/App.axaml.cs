using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using WinterspringLauncher.ViewModels;
using WinterspringLauncher.Views;

namespace WinterspringLauncher;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel() // <-- Will also initialize LauncherLogic
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
