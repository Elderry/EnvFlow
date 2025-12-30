using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
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

            // Populate tree views
            LoadTreeViews();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error initializing window: {ex}");
            throw;
        }
    }

    private void LoadTreeViews()
    {
        // Populate User Variables TreeView
        foreach (var item in ViewModel.UserVariables)
        {
            var treeNode = CreateTreeViewNode(item);
            UserEnvTreeView.RootNodes.Add(treeNode);
        }

        // Populate System Variables TreeView
        foreach (var item in ViewModel.SystemVariables)
        {
            var treeNode = CreateTreeViewNode(item);
            SystemEnvTreeView.RootNodes.Add(treeNode);
        }

        // Update status bar
        StatusTextBlock.Text = ViewModel.StatusMessage;
        UserCountTextBlock.Text = $"User: {ViewModel.UserVariableCount}";
        SystemCountTextBlock.Text = $"System: {ViewModel.SystemVariableCount}";
    }

    private TreeViewNode CreateTreeViewNode(EnvVariableItem item)
    {
        var node = new TreeViewNode();
        
        // Create the content for the node
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        var icon = new FontIcon
        {
            Glyph = item.Icon,
            FontSize = 16,
            Foreground = item.IconColor
        };
        panel.Children.Add(icon);

        var nameText = new TextBlock
        {
            Text = item.DisplayName,
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(nameText);

        if (item.ValueVisibility == Visibility.Visible && !string.IsNullOrEmpty(item.Value))
        {
            var valueText = new TextBlock
            {
                Text = item.Value,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            panel.Children.Add(valueText);
        }

        node.Content = panel;

        // Add children recursively
        foreach (var child in item.Children)
        {
            node.Children.Add(CreateTreeViewNode(child));
        }

        return node;
    }
}
