using System;
using Avalonia;
using Avalonia.Controls;
using WinterspringLauncher.ViewModels;

namespace WinterspringLauncher.Views;

public partial class MainWindow : Window
{
    public new MainWindowViewModel DataContext
    {
        get => base.DataContext as MainWindowViewModel;
        set => base.DataContext = value;
    }

    public MainWindow()
    {
        InitializeComponent();
        LogScroller.PropertyChanged += LogChanged;
    }

    private void LogChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        LogScroller.ScrollToEnd();
    }

    private void ServerSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox)
        {
            DataContext._selectedServerIdx = comboBox.SelectedIndex;
            DataContext.Logic.ChangeServerIdx();
        }
    }
}
