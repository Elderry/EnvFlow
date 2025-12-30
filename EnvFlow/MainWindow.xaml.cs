using System;
using Microsoft.UI.Xaml;
using EnvFlow.ViewModels;

namespace EnvFlow;

public sealed partial class MainWindow : Window
{
    public MainWindowViewModel ViewModel { get; }

    public MainWindow()
    {
        try
        {
            this.InitializeComponent();
            ViewModel = new MainWindowViewModel();
            Title = "EnvFlow - Environment Variable Editor";
            
            // Set window size
            var appWindow = this.AppWindow;
            appWindow.Resize(new Windows.Graphics.SizeInt32(1400, 800));

            // Update status bar
            StatusTextBlock.Text = ViewModel.StatusMessage;
            UserCountTextBlock.Text = $"User: {ViewModel.UserVariableCount}";
            SystemCountTextBlock.Text = $"System: {ViewModel.SystemVariableCount}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error initializing window: {ex}");
            throw;
        }
    }
}
