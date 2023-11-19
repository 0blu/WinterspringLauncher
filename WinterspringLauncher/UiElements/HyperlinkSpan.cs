using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace WinterspringLauncher.UiElements;

public class HyperlinkTextBlock : TextBlock
{
    public static readonly DirectProperty<HyperlinkTextBlock, string> NavigateUriProperty =
        AvaloniaProperty.RegisterDirect<HyperlinkTextBlock, string>(
            nameof(NavigateUri),
            o => o.NavigateUri,
            (o, v) => o.NavigateUri = v);

    private string _navigateUri;

    public string NavigateUri
    {
        get => _navigateUri;
        set => SetAndRaise(NavigateUriProperty, ref _navigateUri, value);
    }

    public HyperlinkTextBlock()
    {
        AddHandler(PointerPressedEvent, OnPointerPressed);
        PseudoClasses.Add(":pointerover");
        Cursor = new Cursor(StandardCursorType.Hand);
        Foreground = Brush.Parse("#2E95D3");
    }

    private void OnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (!string.IsNullOrEmpty(NavigateUri))
        {
            // Open the link here, for example, by launching a browser
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(NavigateUri) { UseShellExecute = true });
        }
    }
}
