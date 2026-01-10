using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using EnvFlow.Dialogs;
using EnvFlow.Helpers;
using EnvFlow.Models;
using EnvFlow.Services;
using EnvFlow.ViewModels;

using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

using Windows.System;
using Windows.UI;

namespace EnvFlow;

public sealed partial class MainWindow : Window
{
    public MainWindowViewModel ViewModel { get; }
    private readonly EnvVarService _envService;
    private bool _isSplitterDragging = false;
    private double _splitterStartX;
    private TreeViewItem? _currentFlyoutTreeItem = null;
    private double _nameColumnWidth = 250;

    public MainWindow(MainWindowViewModel viewModel, EnvVarService envService)
    {
        InitializeComponent();
        ViewModel = viewModel;
        _envService = envService;
        Title = "EnvFlow - Environment Variable Editor";

        // Setup title bar
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(CustomTitleBar);

        AppWindowTitleBar titleBar = AppWindow.TitleBar;
        titleBar.ButtonForegroundColor = Color.FromArgb(255, 0, 0, 0);

        // Set splitter cursor
        SplitterGrid.Cursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);

        // Set column splitter cursors
        UserColumnSplitter.Cursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
        SystemColumnSplitter.Cursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);

        // Update status bar
        UpdateStatusBar();

        // Update UI based on admin status
        UpdateAdminUI();
    }

    private void UpdateStatusBar()
    {
        StatusTextBlock.Text = ViewModel.StatusMessage;
        UserCountTextBlock.Text = $"User: {ViewModel.UserVariableCount}";
        SystemCountTextBlock.Text = $"System: {ViewModel.SystemVariableCount}";
    }

    private void UpdateAdminUI()
    {
        if (ViewModel.IsAdmin)
        {
            SystemVariablesTitle.Text = "System Variables";
            RestartAsAdminButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            SystemVariablesTitle.Text = "System Variables (Read Only)";
            RestartAsAdminButton.Visibility = Visibility.Visible;
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.RefreshVariables();
        UpdateStatusBar();
    }

    private void RestartAsAdminButton_Click(object sender, RoutedEventArgs e)
    {
        AdminHelper.RestartAsAdmin();
    }

    private void UserEnvTreeView_SelectionChanged(object sender, TreeViewSelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is EnvVarItem item)
        {
            ViewModel.SelectedUserVariable = item;
        }
        else
        {
            ViewModel.SelectedUserVariable = null;
        }
    }

    private void SystemEnvTreeView_SelectionChanged(object sender, TreeViewSelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is EnvVarItem item)
        {
            ViewModel.SelectedSystemVariable = item;
        }
        else
        {
            ViewModel.SelectedSystemVariable = null;
        }
    }

    private async void AddUserVariableButton_Click(object sender, RoutedEventArgs e)
    {
        VariableEditorDialog dialog = new()
        {
            Title = "Add User Variable",
            XamlRoot = Content.XamlRoot
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            ViewModel.AddVariable(dialog.VariableName, dialog.VariableValue, isSystemVariable: false);
            UpdateStatusBar();
        }
    }

    private async void AddSystemVariableButton_Click(object sender, RoutedEventArgs e)
    {
        VariableEditorDialog dialog = new()
        {
            Title = "Add System Variable",
            XamlRoot = Content.XamlRoot
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            ViewModel.AddVariable(dialog.VariableName, dialog.VariableValue, isSystemVariable: true);
            UpdateStatusBar();
        }
    }

    private void TreeItem_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is TreeViewItem treeViewItem)
        {
            var hoverButtons = FindChildByName<StackPanel>(treeViewItem, "HoverButtons");
            if (hoverButtons != null)
            {
                hoverButtons.Opacity = 1.0;
            }
        }
    }

    private void TreeItem_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is TreeViewItem treeViewItem)
        {
            // Don't hide buttons if the flyout is currently open for this item
            if (_currentFlyoutTreeItem == treeViewItem)
                return;

            var hoverButtons = FindChildByName<StackPanel>(treeViewItem, "HoverButtons");
            if (hoverButtons != null)
            {
                hoverButtons.Opacity = 0;
            }
        }
    }

    private void MenuFlyout_Opening(object sender, object e)
    {
        // Track which TreeViewItem has its flyout open
        if (sender is MenuFlyout flyout && flyout.Target is Button button)
        {
            // Find the parent TreeViewItem
            DependencyObject parent = button;
            while (parent != null && parent is not TreeViewItem)
            {
                parent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(parent);
            }

            if (parent is TreeViewItem treeViewItem)
            {
                _currentFlyoutTreeItem = treeViewItem;
            }
        }
    }

    private void MenuFlyout_Closed(object sender, object e)
    {
        // Clear the tracked item
        var previousItem = _currentFlyoutTreeItem;
        _currentFlyoutTreeItem = null;

        // Hide hover buttons if pointer is not over the item
        if (previousItem != null)
        {
            var hoverButtons = FindChildByName<StackPanel>(previousItem, "HoverButtons");
            if (hoverButtons != null)
            {
                hoverButtons.Opacity = 0;
            }
        }
    }

    private T? FindChildByName<T>(DependencyObject parent, string childName) where T : FrameworkElement
    {
        int childCount = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild && typedChild.Name == childName)
            {
                return typedChild;
            }

            var result = FindChildByName<T>(child, childName);
            if (result != null)
            {
                return result;
            }
        }
        return null;
    }

    private async void EditButton_Click(object sender, RoutedEventArgs e)
    {
        EnvVarItem item = ((sender as FrameworkElement)?.DataContext as EnvVarItem)!;

        // Open VariableEditorDialog for editing
        if (item.IsEntry)
        {
            // For entry items, open dialog in entry mode
            VariableEditorDialog dialog = new()
            {
                XamlRoot = Content.XamlRoot
            };

            // Find parent variable
            EnvVarItem parent = GetParentVariable(item);

            dialog.ConfigureForEntry(parent.Name, isEditMode: true);
            dialog.VariableValue = item.Name;

            ContentDialogResult result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(dialog.VariableValue))
            {
                ViewModel.UpdateEntry(parent, item, dialog.VariableValue);
                UpdateStatusBar();
            }
        }
        else
        {
            // For parent/single value items, open dialog in normal mode
            VariableEditorDialog dialog = new()
            {
                XamlRoot = Content.XamlRoot,
                Title = "Edit Variable",
                IsEditMode = true,
                VariableName = item.Name,
                VariableValue = item.Value
            };

            ContentDialogResult result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                ViewModel.UpdateVariable(item, dialog.VariableValue);
                UpdateStatusBar();
            }
        }
    }

    private void CopyName_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as MenuFlyoutItem)?.DataContext is not EnvVarItem item)
            return;

        try
        {
            var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dataPackage.SetText(item.Name);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);

            ViewModel.StatusMessage = "Name copied to clipboard";
            UpdateStatusBar();
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Error copying to clipboard: {ex.Message}";
            UpdateStatusBar();
        }
    }

    private void CopyValue_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as MenuFlyoutItem)?.DataContext is not EnvVarItem item)
            return;

        try
        {
            var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dataPackage.SetText(item.IsEntry ? item.Name : item.Value);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);

            ViewModel.StatusMessage = "Value copied to clipboard";
            UpdateStatusBar();
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Error copying to clipboard: {ex.Message}";
            UpdateStatusBar();
        }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        EnvVarItem item = ((sender as FrameworkElement)?.DataContext as EnvVarItem)!;

        // Check if this is a child item
        if (item.IsEntry)
        {
            bool isSystemVariable = ViewModel.SystemVariables.Any(v => v.Children.Contains(item));
            await DeleteEntry(item);
            return;
        }

        // Delete parent variable - show confirmation
        ContentDialog confirmDialog = new()
        {
            Title = "Confirm Delete",
            Content = $"Are you sure you want to delete " +
                      $"the {(item.IsSystemVariable ? "system" : "user")} variable '{item.Name}'?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };

        ContentDialogResult result = await confirmDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            ViewModel.Delete(item);
            UpdateStatusBar();
        }
    }

    private async void TreeViewItem_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        // Get the data context (EnvVariableItem) from the tapped element
        EnvVarItem item = ((sender as FrameworkElement)?.DataContext as EnvVarItem)!;

        // Prevent event from bubbling up
        e.Handled = true;

        // Check if the variable is read-only (volatile)
        if (item.IsReadOnly)
        {
            ViewModel.StatusMessage = "This is a read-only system-managed variable";
            UpdateStatusBar();
            return;
        }

        // Check if the variable is composite (has children)
        if (item.IsComposite)
        {
            return;
        }

        // Check admin permissions for system variables
        if (item.IsSystemVariable && !ViewModel.IsAdmin)
        {
            ViewModel.StatusMessage = "Administrator privileges required to edit system variables";
            UpdateStatusBar();
            return;
        }

        // Enter edit mode
        item.EditValue = item.IsEntry ? item.Name : item.Value;
        item.IsEditing = true;

        // Find the TextBox and focus it
        TreeViewItem treeViewItem = FindAncestor<TreeViewItem>(sender as DependencyObject)!;
        TextBox textBox = FindChildByName<TextBox>(treeViewItem, item.IsEntry ? "ChildEditTextBox" : "EditTextBox")!;
        textBox.Focus(FocusState.Programmatic);
    }

    private static T? FindAncestor<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            child = VisualTreeHelper.GetParent(child);
            if (child is T ancestor)
                return ancestor;
        }
        return null;
    }

    private void EditTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        EnvVarItem item = ((sender as TextBox)?.DataContext as EnvVarItem)!;

        if (e.Key == VirtualKey.Enter)
        {
            // Save changes
            e.Handled = true;
            SaveInlineEdit(item);
        }
        else if (e.Key == VirtualKey.Escape)
        {
            // Cancel editing
            e.Handled = true;
            item.IsEditing = false;
        }
    }

    /// <summary>
    /// Save when focus is lost
    /// </summary>
    private void EditTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        EnvVarItem item = ((sender as TextBox)?.DataContext as EnvVarItem)!;
        SaveInlineEdit(item);
    }

    private void SaveInlineEdit(EnvVarItem item)
    {
        // Exit edit mode
        item.IsEditing = false;

        // Check if value changed
        string originalValue = item.IsEntry ? item.Name : item.Value;
        if (item.EditValue == originalValue)
        {
            return;
        }

        // Save changes
        if (item.IsEntry)
        {
            // Find parent variable
            EnvVarItem parentVariable = GetParentVariable(item);

            ViewModel.UpdateEntry(parentVariable, item, item.EditValue);
        }
        else
        {
            ViewModel.UpdateVariable(item, item.EditValue);
        }

        UpdateStatusBar();
    }

    private bool IsInUserTreeView(EnvVarItem item)
    {
        // Check if the item exists in the user variables collection
        return ViewModel.UserVariables.Any(v => v == item || v.Children.Contains(item));
    }

    private EnvVarItem GetParentVariable(EnvVarItem entry)
    {
        ObservableCollection<EnvVarItem> collection = entry.IsSystemVariable
            ? ViewModel.SystemVariables
            : ViewModel.UserVariables;
        return collection.First(v => v.Children.Contains(entry));
    }

    private async void AddEntryButton_Click(object sender, RoutedEventArgs e)
    {
        EnvVarItem item = ((sender as FrameworkElement)?.DataContext as EnvVarItem)!;

        // Use the VariableEditorDialog in path entry mode
        VariableEditorDialog dialog = new()
        {
            XamlRoot = Content.XamlRoot
        };
        dialog.ConfigureForEntry(item.Name);

        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(dialog.VariableValue))
        {
            ViewModel.AddEntry(item, dialog.VariableValue);
            UpdateStatusBar();
        }
    }

    private void SortMenu_Click(object sender, RoutedEventArgs e)
    {
        EnvVarItem item = ((sender as FrameworkElement)?.DataContext as EnvVarItem)!;
        ViewModel.Sort(item);
        UpdateStatusBar();
    }

    private void ShrinkMenu_Click(object sender, RoutedEventArgs _)
    {
        EnvVarItem item = ((sender as MenuFlyoutItem)?.DataContext as EnvVarItem)!;
        ViewModel.Shrink(item);
        UpdateStatusBar();
    }

    private void ExpandMenu_Click(object sender, RoutedEventArgs _)
    {
        EnvVarItem item = ((sender as MenuFlyoutItem)?.DataContext as EnvVarItem)!;
        ViewModel.Expand(item);
        UpdateStatusBar();
    }

    private async Task DeleteEntry(EnvVarItem entry)
    {
        // Find parent variable
        EnvVarItem parentVariable = GetParentVariable(entry);

        // Confirm deletion
        ContentDialog confirmDialog = new()
        {
            Title = "Confirm Delete",
            Content = $"Are you sure you want to delete this entry?\n\n{entry.Name}",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };

        ContentDialogResult result = await confirmDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            ViewModel.DeleteEntry(parentVariable, entry);
            UpdateStatusBar();
        }
    }

    // Splitter drag functionality
    private void Splitter_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        // Cursor change to SizeWestEast handled by system when dragging
    }

    private void Splitter_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        // Cursor restored by system
    }

    private void Splitter_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Grid splitter)
        {
            _isSplitterDragging = true;
            _splitterStartX = e.GetCurrentPoint(null).Position.X;
            splitter.CapturePointer(e.Pointer);
            e.Handled = true;
        }
    }

    private void Splitter_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_isSplitterDragging && sender is Grid splitter)
        {
            var currentPoint = e.GetCurrentPoint(null).Position.X;
            var delta = currentPoint - _splitterStartX;

            // Get the parent grid
            if (splitter.Parent is Grid parentGrid && parentGrid.ColumnDefinitions.Count >= 3)
            {
                var leftColumn = parentGrid.ColumnDefinitions[0];
                var rightColumn = parentGrid.ColumnDefinitions[2];

                // Calculate new widths
                var leftWidth = leftColumn.ActualWidth + delta;
                var rightWidth = rightColumn.ActualWidth - delta;

                // Enforce minimum widths
                if (leftWidth >= 300 && rightWidth >= 300)
                {
                    leftColumn.Width = new GridLength(leftWidth, GridUnitType.Pixel);
                    rightColumn.Width = new GridLength(rightWidth, GridUnitType.Pixel);
                    _splitterStartX = currentPoint;
                }
            }
            e.Handled = true;
        }
    }

    private void Splitter_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_isSplitterDragging && sender is Grid splitter)
        {
            _isSplitterDragging = false;
            splitter.ReleasePointerCaptures();

            // Convert pixel widths back to star sizing for responsive behavior
            if (splitter.Parent is Grid parentGrid && parentGrid.ColumnDefinitions.Count >= 3)
            {
                var leftColumn = parentGrid.ColumnDefinitions[0];
                var rightColumn = parentGrid.ColumnDefinitions[2];

                var totalWidth = leftColumn.ActualWidth + rightColumn.ActualWidth;
                var leftRatio = leftColumn.ActualWidth / totalWidth;
                var rightRatio = rightColumn.ActualWidth / totalWidth;

                leftColumn.Width = new GridLength(leftRatio, GridUnitType.Star);
                rightColumn.Width = new GridLength(rightRatio, GridUnitType.Star);
            }

            e.Handled = true;
        }
    }

    // Column Splitter Handlers
    private bool _isColumnSplitterDragging = false;
    private double _columnSplitterStartX;
    private double _columnSplitterStartWidth;
    private ColumnDefinition? _resizingColumn;

    private void ColumnSplitter_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is UIElement splitter)
        {
            _isColumnSplitterDragging = true;
            var point = e.GetCurrentPoint(null);  // Get screen coordinates
            _columnSplitterStartX = point.Position.X;

            // Determine which column to resize based on Tag
            var tag = (sender as FrameworkElement)?.Tag as string;
            _resizingColumn = tag == "User" ? UserNameColumn : SystemNameColumn;
            _columnSplitterStartWidth = _resizingColumn.ActualWidth;  // Use ActualWidth for current rendered width

            splitter.CapturePointer(e.Pointer);
            e.Handled = true;
        }
    }

    private void ColumnSplitter_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_isColumnSplitterDragging && _resizingColumn != null)
        {
            var point = e.GetCurrentPoint(null);  // Get screen coordinates
            var currentX = point.Position.X;
            var delta = currentX - _columnSplitterStartX;

            var newWidth = _columnSplitterStartWidth + delta;
            if (newWidth >= 100 && newWidth <= 600)  // Min and max width constraints
            {
                _resizingColumn.Width = new GridLength(newWidth);

                // Update all TreeViewItem column widths to match
                var tag = (sender as FrameworkElement)?.Tag as string;
                var treeView = tag == "User" ? UserEnvTreeView : SystemEnvTreeView;
                UpdateTreeViewColumnWidths(treeView, newWidth);
            }

            e.Handled = true;
        }
    }

    private void UpdateTreeViewColumnWidths(TreeView treeView, double width)
    {
        _nameColumnWidth = width;

        // Recursively update all TreeViewItems
        UpdateContainersColumnWidth(treeView, width);

        // Force layout update
        treeView.UpdateLayout();
    }

    private void UpdateContainersColumnWidth(DependencyObject parent, double width)
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            // If we find a Grid with exactly 4 column definitions (our item template structure)
            if (child is Grid grid && grid.ColumnDefinitions.Count == 4)
            {
                // Update the first column (Name column)
                grid.ColumnDefinitions[0].Width = new GridLength(width);
            }

            // Continue searching deeper
            UpdateContainersColumnWidth(child, width);
        }
    }

    private void ColumnSplitter_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_isColumnSplitterDragging && sender is UIElement splitter)
        {
            _isColumnSplitterDragging = false;
            _resizingColumn = null;
            splitter.ReleasePointerCaptures();
            e.Handled = true;
        }
    }

    private void NameColumn_Loaded(object sender, RoutedEventArgs e)
    {
        StackPanel panel = (sender as StackPanel)!;
        Grid parent = (panel.Parent as Grid)!;
        EnvVarItem item = (panel.DataContext as EnvVarItem)!;

        // Entries and parents should span all columns
        if (item.IsEntry || item.IsComposite)
        {
            Grid.SetColumnSpan(parent, 3);
        }
    }

    private void HoverButtons_Loaded(object sender, RoutedEventArgs e)
    {
        StackPanel panel = (sender as StackPanel)!;

        // Gray out and disable buttons if not admin (for system variables)
        if (!ViewModel.IsAdmin)
        {
            foreach (UIElement child in panel.Children)
            {
                Button button = (child as Button)!;
                FontIcon icon = (button.Content as FontIcon)!;
                icon.Foreground = new SolidColorBrush(Colors.Gray);
                button.IsEnabled = false;
            }
        }
    }
}
