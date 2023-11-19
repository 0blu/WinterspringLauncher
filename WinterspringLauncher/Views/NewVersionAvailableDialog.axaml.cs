using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using WinterspringLauncher.UiElements;

namespace WinterspringLauncher.Views;

public partial class NewVersionAvailableDialog : Window
{
    public string NewVersion { get; set; }

    public NewVersionAvailableDialog(LauncherVersion.UpdateInformation updateInformation)
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif

        TextBlock version = this.Find<TextBlock>("VersionIndicator")!;
        HyperlinkTextBlock dlLinkIndicator = this.Find<HyperlinkTextBlock>("DlLinkIndicator")!;

        version.Text = updateInformation.VersionName;
        dlLinkIndicator.NavigateUri = updateInformation.URLLinkToReleasePage;
        dlLinkIndicator.Text = updateInformation.URLLinkToReleasePage;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void CloseButtonClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
