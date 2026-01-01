using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Input;
using EnvFlow.ViewModels;
using EnvFlow.Helpers;
using EnvFlow.Dialogs;

namespace EnvFlow;

public sealed partial class MainWindow : Window
{
    public MainWindowViewModel ViewModel { get; }
    private EnvVariableItem? _currentlyEditingItem;
    private bool _isSplitterDragging = false;
    private double _splitterStartX;
    private TreeViewItem? _currentFlyoutTreeItem = null;
    private double _nameColumnWidth = 250;

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

            // Customize title bar
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(CustomTitleBar);

            var titleBar = AppWindow.TitleBar;
            titleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
            titleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 0, 0, 0);
            titleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
            titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 102, 102, 102);

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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error initializing window: {ex}");
            throw;
        }
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
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is EnvVariableItem item)
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
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is EnvVariableItem item)
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
        var dialog = new VariableEditorDialog
        {
            Title = "Add User Variable",
            XamlRoot = this.Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            try
            {
                ViewModel.StatusMessage = $"Adding user variable: {dialog.VariableName}";
                var service = new Services.EnvironmentVariableService();
                service.SetUserVariable(dialog.VariableName, dialog.VariableValue);
                ViewModel.RefreshVariables();
                UpdateStatusBar();
                ViewModel.StatusMessage = $"Added user variable: {dialog.VariableName}";
            }
            catch (Exception ex)
            {
                ViewModel.StatusMessage = $"Error adding variable: {ex.Message}";
                UpdateStatusBar();
            }
        }
    }

    private async void AddSystemVariableButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsAdmin)
        {
            ViewModel.StatusMessage = "Administrator privileges required to add system variables";
            UpdateStatusBar();
            return;
        }

        var dialog = new VariableEditorDialog
        {
            Title = "Add System Variable",
            XamlRoot = this.Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            try
            {
                ViewModel.StatusMessage = $"Adding system variable: {dialog.VariableName}";
                var service = new Services.EnvironmentVariableService();
                service.SetSystemVariable(dialog.VariableName, dialog.VariableValue);
                ViewModel.RefreshVariables();
                UpdateStatusBar();
                ViewModel.StatusMessage = $"Added system variable: {dialog.VariableName}";
            }
            catch (Exception ex)
            {
                ViewModel.StatusMessage = $"Error adding variable: {ex.Message}";
                UpdateStatusBar();
            }
        }
    }

    private void EditUserVariableButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedUserVariable == null)
        {
            ViewModel.StatusMessage = "Please select a variable to edit";
            UpdateStatusBar();
            return;
        }

        // Exit edit mode for previously editing item
        if (_currentlyEditingItem != null && _currentlyEditingItem != ViewModel.SelectedUserVariable)
        {
            _currentlyEditingItem.IsEditing = false;
        }

        // Enter inline edit mode
        _currentlyEditingItem = ViewModel.SelectedUserVariable;
        ViewModel.SelectedUserVariable.EditValue = ViewModel.SelectedUserVariable.IsChild 
            ? ViewModel.SelectedUserVariable.DisplayName 
            : ViewModel.SelectedUserVariable.Value;
        ViewModel.SelectedUserVariable.IsEditing = true;
        ViewModel.StatusMessage = "Editing variable inline. Press Enter to save, Escape to cancel.";
        UpdateStatusBar();
    }

    private void EditSystemVariableButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsAdmin)
        {
            ViewModel.StatusMessage = "Administrator privileges required to edit system variables";
            UpdateStatusBar();
            return;
        }

        if (ViewModel.SelectedSystemVariable == null)
        {
            ViewModel.StatusMessage = "Please select a variable to edit";
            UpdateStatusBar();
            return;
        }

        // Exit edit mode for previously editing item
        if (_currentlyEditingItem != null && _currentlyEditingItem != ViewModel.SelectedSystemVariable)
        {
            _currentlyEditingItem.IsEditing = false;
        }

        // Enter inline edit mode
        _currentlyEditingItem = ViewModel.SelectedSystemVariable;
        ViewModel.SelectedSystemVariable.EditValue = ViewModel.SelectedSystemVariable.IsChild 
            ? ViewModel.SelectedSystemVariable.DisplayName 
            : ViewModel.SelectedSystemVariable.Value;
        ViewModel.SelectedSystemVariable.IsEditing = true;
        ViewModel.StatusMessage = "Editing variable inline. Press Enter to save, Escape to cancel.";
        UpdateStatusBar();
    }

    private async void DeleteUserVariableButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedUserVariable == null)
        {
            ViewModel.StatusMessage = "Please select a variable to delete";
            UpdateStatusBar();
            return;
        }

        // Check if this is a child item
        if (ViewModel.SelectedUserVariable.IsChild)
        {
            await DeleteChildEntry(ViewModel.SelectedUserVariable, false);
            return;
        }

        var confirmDialog = new ContentDialog
        {
            Title = "Confirm Delete",
            Content = $"Are you sure you want to delete the user variable '{ViewModel.SelectedUserVariable.Name}'?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.Content.XamlRoot
        };

        var result = await confirmDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            ViewModel.DeleteUserVariable();
            UpdateStatusBar();
        }
    }

    private async void DeleteSystemVariableButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsAdmin)
        {
            ViewModel.StatusMessage = "Administrator privileges required to delete system variables";
            UpdateStatusBar();
            return;
        }

        if (ViewModel.SelectedSystemVariable == null)
        {
            ViewModel.StatusMessage = "Please select a variable to delete";
            UpdateStatusBar();
            return;
        }

        // Check if this is a child item
        if (ViewModel.SelectedSystemVariable.IsChild)
        {
            await DeleteChildEntry(ViewModel.SelectedSystemVariable, true);
            return;
        }

        var confirmDialog = new ContentDialog
        {
            Title = "Confirm Delete",
            Content = $"Are you sure you want to delete the system variable '{ViewModel.SelectedSystemVariable.Name}'?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.Content.XamlRoot
        };

        var result = await confirmDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            ViewModel.DeleteSystemVariable();
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

    private void EditItemButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not EnvVariableItem item)
            return;

        // Check if the variable is read-only
        if (item.IsReadOnly)
        {
            ViewModel.StatusMessage = "This is a read-only system-managed variable";
            UpdateStatusBar();
            return;
        }

        // Determine if this is a system variable
        bool isSystemVariable = ViewModel.SystemVariables.Contains(item);
        if (!isSystemVariable)
        {
            foreach (var sysVar in ViewModel.SystemVariables)
            {
                if (sysVar.Children.Contains(item))
                {
                    isSystemVariable = true;
                    break;
                }
            }
        }

        // Check admin permissions for system variables
        if (isSystemVariable && !ViewModel.IsAdmin)
        {
            ViewModel.StatusMessage = "Administrator privileges required to edit system variables";
            UpdateStatusBar();
            return;
        }

        // Exit edit mode for previously editing item
        if (_currentlyEditingItem != null && _currentlyEditingItem != item)
        {
            _currentlyEditingItem.IsEditing = false;
        }

        // Enter inline edit mode
        _currentlyEditingItem = item;
        item.EditValue = item.IsChild ? item.DisplayName : item.Value;
        item.IsEditing = true;
        ViewModel.StatusMessage = "Editing variable inline. Press Enter to save, Escape to cancel.";
        UpdateStatusBar();
    }

    private void CopyName_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as MenuFlyoutItem)?.DataContext is not EnvVariableItem item)
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
        if ((sender as MenuFlyoutItem)?.DataContext is not EnvVariableItem item)
            return;

        try
        {
            var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dataPackage.SetText(item.IsChild ? item.DisplayName : item.Value);
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

    private async void DeleteItemButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not EnvVariableItem item)
            return;

        // Check if the variable is read-only
        if (item.IsReadOnly)
        {
            ViewModel.StatusMessage = "This is a read-only system-managed variable";
            UpdateStatusBar();
            return;
        }

        // Determine if this is a system variable
        bool isSystemVariable = ViewModel.SystemVariables.Contains(item);
        if (!isSystemVariable)
        {
            foreach (var sysVar in ViewModel.SystemVariables)
            {
                if (sysVar.Children.Contains(item))
                {
                    isSystemVariable = true;
                    break;
                }
            }
        }

        // Check admin permissions for system variables
        if (isSystemVariable && !ViewModel.IsAdmin)
        {
            ViewModel.StatusMessage = "Administrator privileges required to delete system variables";
            UpdateStatusBar();
            return;
        }

        // Check if this is a child item
        if (item.IsChild)
        {
            await DeleteChildEntry(item, isSystemVariable);
            return;
        }

        // Delete parent variable
        var confirmDialog = new ContentDialog
        {
            Title = "Confirm Delete",
            Content = $"Are you sure you want to delete the {(isSystemVariable ? "system" : "user")} variable '{item.Name}'?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.Content.XamlRoot
        };

        var result = await confirmDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            if (isSystemVariable)
            {
                ViewModel.SelectedSystemVariable = item;
                ViewModel.DeleteSystemVariable();
            }
            else
            {
                ViewModel.SelectedUserVariable = item;
                ViewModel.DeleteUserVariable();
            }
            UpdateStatusBar();
        }
    }

    private async void TreeViewItem_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        // Get the data context (EnvVariableItem) from the tapped element
        if ((sender as FrameworkElement)?.DataContext is not EnvVariableItem item)
            return;

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

        // Determine if this is a user or system variable
        bool isSystemVariable = ViewModel.SystemVariables.Contains(item);
        if (!isSystemVariable)
        {
            // Check if it's a child of a system variable
            foreach (var sysVar in ViewModel.SystemVariables)
            {
                if (sysVar.Children.Contains(item))
                {
                    isSystemVariable = true;
                    break;
                }
            }
        }

        // Check admin permissions for system variables
        if (isSystemVariable && !ViewModel.IsAdmin)
        {
            ViewModel.StatusMessage = "Administrator privileges required to edit system variables";
            UpdateStatusBar();
            return;
        }

        // Exit edit mode for previously editing item
        if (_currentlyEditingItem != null && _currentlyEditingItem != item)
        {
            _currentlyEditingItem.IsEditing = false;
        }

        // Enter edit mode
        _currentlyEditingItem = item;
        item.EditValue = item.IsChild ? item.DisplayName : item.Value;
        item.IsEditing = true;
    }

    private void EditTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        // Focus and select all when TextBox appears
        if (sender is TextBox textBox)
        {
            textBox.Focus(FocusState.Programmatic);
            textBox.SelectAll();
        }
    }

    private void EditTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.DataContext is not EnvVariableItem item)
            return;

        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            // Save changes
            e.Handled = true;
            SaveInlineEdit(item);
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            // Cancel editing
            e.Handled = true;
            item.IsEditing = false;
            
            // Clear currently editing item
            if (_currentlyEditingItem == item)
            {
                _currentlyEditingItem = null;
            }
        }
    }

    private void EditTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        // Save when focus is lost
        if (sender is TextBox textBox && textBox.DataContext is EnvVariableItem item)
        {
            SaveInlineEdit(item);
        }
    }

    private void SaveInlineEdit(EnvVariableItem item)
    {
        if (!item.IsEditing) return;

        // Exit edit mode
        item.IsEditing = false;
        
        // Clear currently editing item
        if (_currentlyEditingItem == item)
        {
            _currentlyEditingItem = null;
        }

        // Check if value changed
        string originalValue = item.IsChild ? item.DisplayName : item.Value;
        if (item.EditValue == originalValue)
            return;

        // Determine if this is a user or system variable (or child of one)
        bool isSystemVariable = ViewModel.SystemVariables.Contains(item);
        EnvVariableItem? parentVariable = null;
        
        if (item.IsChild)
        {
            // Find parent variable
            foreach (var userVar in ViewModel.UserVariables)
            {
                if (userVar.Children.Contains(item))
                {
                    parentVariable = userVar;
                    isSystemVariable = false;
                    break;
                }
            }
            
            if (parentVariable == null)
            {
                foreach (var sysVar in ViewModel.SystemVariables)
                {
                    if (sysVar.Children.Contains(item))
                    {
                        parentVariable = sysVar;
                        isSystemVariable = true;
                        break;
                    }
                }
            }
            
            if (parentVariable == null) return;
        }

        try
        {
            var service = new Services.EnvironmentVariableService();
            
            if (item.IsChild && parentVariable != null)
            {
                // Update the child and reconstruct the parent PATH variable
                var paths = parentVariable.Children.Select(c => c == item ? item.EditValue : c.DisplayName).ToList();
                string newValue = string.Join(";", paths);
                
                ViewModel.StatusMessage = $"Updating {(isSystemVariable ? "system" : "user")} path entry in {parentVariable.Name}";
                
                if (isSystemVariable)
                    service.SetSystemVariable(parentVariable.Name, newValue);
                else
                    service.SetUserVariable(parentVariable.Name, newValue);
            }
            else
            {
                ViewModel.StatusMessage = $"Updating {(isSystemVariable ? "system" : "user")} variable: {item.Name}";
                
                if (isSystemVariable)
                    service.SetSystemVariable(item.Name, item.EditValue);
                else
                    service.SetUserVariable(item.Name, item.EditValue);
            }

            ViewModel.RefreshVariables();
            UpdateStatusBar();
            ViewModel.StatusMessage = $"Updated {(isSystemVariable ? "system" : "user")} variable successfully";
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Error updating variable: {ex.Message}";
            UpdateStatusBar();
        }
    }

    private bool IsInUserTreeView(EnvVariableItem item)
    {
        // Check if the item exists in the user variables collection
        return ViewModel.UserVariables.Any(v => v == item || v.Children.Contains(item));
    }

    private EnvVariableItem? GetParentVariable(EnvVariableItem childItem, bool isUserVariable)
    {
        var collection = isUserVariable ? ViewModel.UserVariables : ViewModel.SystemVariables;
        return collection.FirstOrDefault(v => v.Children.Contains(childItem));
    }

    private async void AddChildButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not EnvVariableItem parentItem)
            return;

        // Determine if this is a user or system variable
        bool isSystemVariable = ViewModel.SystemVariables.Contains(parentItem);

        // Check admin permissions for system variables
        if (isSystemVariable && !ViewModel.IsAdmin)
        {
            ViewModel.StatusMessage = "Administrator privileges required to modify system variables";
            UpdateStatusBar();
            return;
        }

        // Use the VariableEditorDialog in path entry mode
        var dialog = new Dialogs.VariableEditorDialog
        {
            XamlRoot = this.Content.XamlRoot
        };
        
        dialog.ConfigureForPathEntry(parentItem.Name);

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(dialog.VariableValue))
        {
            try
            {
                var service = new Services.EnvironmentVariableService();
                
                // Add the new path to the existing paths
                var existingPaths = parentItem.Children.Select(c => c.DisplayName).ToList();
                existingPaths.Add(dialog.VariableValue.Trim());
                string newValue = string.Join(";", existingPaths);
                
                ViewModel.StatusMessage = $"Adding path entry to {parentItem.Name}";
                
                if (isSystemVariable)
                    service.SetSystemVariable(parentItem.Name, newValue);
                else
                    service.SetUserVariable(parentItem.Name, newValue);
                
                ViewModel.RefreshVariables();
                UpdateStatusBar();
                ViewModel.StatusMessage = $"Added path entry to {parentItem.Name}";
            }
            catch (Exception ex)
            {
                ViewModel.StatusMessage = $"Error adding path entry: {ex.Message}";
                UpdateStatusBar();
            }
        }
    }

    private async void NormalizeButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not EnvVariableItem parentItem)
            return;

        // Determine if this is a user or system variable
        bool isSystemVariable = ViewModel.SystemVariables.Contains(parentItem);

        // Check admin permissions for system variables
        if (isSystemVariable && !ViewModel.IsAdmin)
        {
            ViewModel.StatusMessage = "Administrator privileges required to modify system variables";
            UpdateStatusBar();
            return;
        }

        try
        {
            var service = new Services.EnvironmentVariableService();
            
            // Get all environment variables for substitution
            var allVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            
            // Add user variables
            foreach (var item in ViewModel.UserVariables)
            {
                if (!item.IsChild && !string.IsNullOrEmpty(item.Value))
                {
                    allVars[item.Name] = Environment.ExpandEnvironmentVariables(item.Value);
                }
            }
            
            // Add system variables
            foreach (var item in ViewModel.SystemVariables)
            {
                if (!item.IsChild && !string.IsNullOrEmpty(item.Value))
                {
                    allVars[item.Name] = Environment.ExpandEnvironmentVariables(item.Value);
                }
            }
            
            // Normalize and sort paths
            var normalizedPaths = new List<string>();
            
            foreach (var child in parentItem.Children)
            {
                var path = child.DisplayName.Trim();
                var expandedPath = Environment.ExpandEnvironmentVariables(path);
                
                // Try to find a matching environment variable
                string bestMatch = null;
                int longestMatchLength = 0;
                
                foreach (var kvp in allVars)
                {
                    var varValue = kvp.Value.TrimEnd('\\');
                    
                    // Check if path starts with this variable's value
                    if (expandedPath.StartsWith(varValue, StringComparison.OrdinalIgnoreCase) && 
                        varValue.Length > longestMatchLength)
                    {
                        bestMatch = kvp.Key;
                        longestMatchLength = varValue.Length;
                    }
                }
                
                // Replace with variable reference if found
                string normalizedPath;
                if (bestMatch != null)
                {
                    var varValue = allVars[bestMatch].TrimEnd('\\');
                    var remainder = expandedPath.Substring(varValue.Length).TrimStart('\\');
                    normalizedPath = string.IsNullOrEmpty(remainder) 
                        ? $"%{bestMatch}%" 
                        : $"%{bestMatch}%\\{remainder}";
                }
                else
                {
                    normalizedPath = path;
                }
                
                normalizedPaths.Add(normalizedPath);
            }
            
            // Sort paths
            normalizedPaths.Sort(StringComparer.OrdinalIgnoreCase);
            
            // Save the normalized and sorted value
            string newValue = string.Join(";", normalizedPaths);
            
            ViewModel.StatusMessage = $"Normalizing {parentItem.Name}";
            
            if (isSystemVariable)
                service.SetSystemVariable(parentItem.Name, newValue);
            else
                service.SetUserVariable(parentItem.Name, newValue);
            
            ViewModel.RefreshVariables();
            UpdateStatusBar();
            ViewModel.StatusMessage = $"Normalized {parentItem.Name}";
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Error normalizing: {ex.Message}";
            UpdateStatusBar();
        }
    }

    private async void SortButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not EnvVariableItem parentItem)
            return;

        // Determine if this is a user or system variable
        bool isSystemVariable = ViewModel.SystemVariables.Contains(parentItem);

        // Check admin permissions for system variables
        if (isSystemVariable && !ViewModel.IsAdmin)
        {
            ViewModel.StatusMessage = "Administrator privileges required to modify system variables";
            UpdateStatusBar();
            return;
        }

        try
        {
            var service = new Services.EnvironmentVariableService();
            
            // Get all paths and sort them
            var paths = parentItem.Children.Select(c => c.DisplayName.Trim()).ToList();
            paths.Sort(StringComparer.OrdinalIgnoreCase);
            
            // Save the sorted value
            string newValue = string.Join(";", paths);
            
            ViewModel.StatusMessage = $"Sorting {parentItem.Name}";
            
            if (isSystemVariable)
                service.SetSystemVariable(parentItem.Name, newValue);
            else
                service.SetUserVariable(parentItem.Name, newValue);
            
            ViewModel.RefreshVariables();
            UpdateStatusBar();
            ViewModel.StatusMessage = $"Sorted {parentItem.Name}";
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Error sorting: {ex.Message}";
            UpdateStatusBar();
        }
    }

    private async void ShrinkButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as MenuFlyoutItem)?.DataContext is not EnvVariableItem parentItem)
            return;

        // Determine if this is a user or system variable
        bool isSystemVariable = ViewModel.SystemVariables.Contains(parentItem);

        // Check admin permissions for system variables
        if (isSystemVariable && !ViewModel.IsAdmin)
        {
            ViewModel.StatusMessage = "Administrator privileges required to modify system variables";
            UpdateStatusBar();
            return;
        }

        try
        {
            var service = new Services.EnvironmentVariableService();
            
            // Get all environment variables for substitution
            var allVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            
            // Add all read-only variables (volatile, system-defined)
            foreach (var item in ViewModel.UserVariables)
            {
                if (!item.IsChild && !string.IsNullOrEmpty(item.Value) && item.IsReadOnly)
                {
                    // Get the environment variable from the current process (fully expanded)
                    var varValue = Environment.GetEnvironmentVariable(item.Name);
                    if (!string.IsNullOrEmpty(varValue))
                    {
                        allVars[item.Name] = varValue;
                    }
                }
            }
            
            foreach (var item in ViewModel.SystemVariables)
            {
                if (!item.IsChild && !string.IsNullOrEmpty(item.Value) && item.IsReadOnly)
                {
                    // Get the environment variable from the current process (fully expanded)
                    var varValue = Environment.GetEnvironmentVariable(item.Name);
                    if (!string.IsNullOrEmpty(varValue))
                    {
                        allVars[item.Name] = varValue;
                    }
                }
            }
            
            // Shrink paths by replacing with variable references
            var shrunkPaths = new List<string>();
            
            foreach (var child in parentItem.Children)
            {
                var path = child.DisplayName.Trim();
                var expandedPath = Environment.ExpandEnvironmentVariables(path);
                
                // Try to find a matching environment variable
                string bestMatch = null;
                int longestMatchLength = 0;
                
                foreach (var kvp in allVars)
                {
                    var varValue = kvp.Value.TrimEnd('\\');
                    
                    // Check if path starts with this variable's value
                    if (expandedPath.StartsWith(varValue, StringComparison.OrdinalIgnoreCase) && 
                        varValue.Length > longestMatchLength)
                    {
                        bestMatch = kvp.Key;
                        longestMatchLength = varValue.Length;
                    }
                }
                
                // Replace with variable reference if found
                string shrunkPath;
                if (bestMatch != null)
                {
                    var varValue = allVars[bestMatch].TrimEnd('\\');
                    var remainder = expandedPath.Substring(varValue.Length).TrimStart('\\');
                    shrunkPath = string.IsNullOrEmpty(remainder) 
                        ? $"%{bestMatch}%" 
                        : $"%{bestMatch}%\\{remainder}";
                }
                else
                {
                    shrunkPath = path;
                }
                
                shrunkPaths.Add(shrunkPath);
            }
            
            // Save the shrunk value
            string newValue = string.Join(";", shrunkPaths);
            
            ViewModel.StatusMessage = $"Shrinking {parentItem.Name}";
            
            if (isSystemVariable)
                service.SetSystemVariable(parentItem.Name, newValue);
            else
                service.SetUserVariable(parentItem.Name, newValue);
            
            ViewModel.RefreshVariables();
            UpdateStatusBar();
            ViewModel.StatusMessage = $"Shrunk paths in {parentItem.Name}";
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Error shrinking: {ex.Message}";
            UpdateStatusBar();
        }
    }

    private async void ExpandButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as MenuFlyoutItem)?.DataContext is not EnvVariableItem parentItem)
            return;

        // Determine if this is a user or system variable
        bool isSystemVariable = ViewModel.SystemVariables.Contains(parentItem);

        // Check admin permissions for system variables
        if (isSystemVariable && !ViewModel.IsAdmin)
        {
            ViewModel.StatusMessage = "Administrator privileges required to modify system variables";
            UpdateStatusBar();
            return;
        }

        try
        {
            var service = new Services.EnvironmentVariableService();
            
            // Expand all paths
            var expandedPaths = new List<string>();
            
            foreach (var child in parentItem.Children)
            {
                var path = child.DisplayName.Trim();
                var expandedPath = Environment.ExpandEnvironmentVariables(path);
                expandedPaths.Add(expandedPath);
            }
            
            // Save the expanded value
            string newValue = string.Join(";", expandedPaths);
            
            ViewModel.StatusMessage = $"Expanding {parentItem.Name}";
            
            if (isSystemVariable)
                service.SetSystemVariable(parentItem.Name, newValue);
            else
                service.SetUserVariable(parentItem.Name, newValue);
            
            ViewModel.RefreshVariables();
            UpdateStatusBar();
            ViewModel.StatusMessage = $"Expanded paths in {parentItem.Name}";
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Error expanding: {ex.Message}";
            UpdateStatusBar();
        }
    }

    private async void ShrinkValueButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as MenuFlyoutItem)?.DataContext is not EnvVariableItem item)
            return;

        // Determine if this is a user or system variable
        bool isSystemVariable = ViewModel.SystemVariables.Contains(item);

        // Check admin permissions for system variables
        if (isSystemVariable && !ViewModel.IsAdmin)
        {
            ViewModel.StatusMessage = "Administrator privileges required to modify system variables";
            UpdateStatusBar();
            return;
        }

        try
        {
            var service = new Services.EnvironmentVariableService();
            
            // Get all environment variables for substitution
            var allVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            
            // Add all read-only variables (volatile, system-defined)
            foreach (var userVar in ViewModel.UserVariables)
            {
                if (!userVar.IsChild && !string.IsNullOrEmpty(userVar.Value) && userVar.IsReadOnly)
                {
                    // Get the environment variable from the current process (fully expanded)
                    var varValue = Environment.GetEnvironmentVariable(userVar.Name);
                    if (!string.IsNullOrEmpty(varValue))
                    {
                        allVars[userVar.Name] = varValue;
                    }
                }
            }
            
            foreach (var sysVar in ViewModel.SystemVariables)
            {
                if (!sysVar.IsChild && !string.IsNullOrEmpty(sysVar.Value) && sysVar.IsReadOnly)
                {
                    // Get the environment variable from the current process (fully expanded)
                    var varValue = Environment.GetEnvironmentVariable(sysVar.Name);
                    if (!string.IsNullOrEmpty(varValue))
                    {
                        allVars[sysVar.Name] = varValue;
                    }
                }
            }
            
            // Expand the current value
            var expandedValue = Environment.ExpandEnvironmentVariables(item.Value);
            
            // Try to find a matching environment variable
            string bestMatch = null;
            int longestMatchLength = 0;
            
            foreach (var kvp in allVars)
            {
                // Skip the variable itself
                if (kvp.Key.Equals(item.Name, StringComparison.OrdinalIgnoreCase))
                    continue;
                    
                var varValue = kvp.Value.TrimEnd('\\');
                
                // Check if value starts with this variable's value
                if (expandedValue.StartsWith(varValue, StringComparison.OrdinalIgnoreCase) && 
                    varValue.Length > longestMatchLength)
                {
                    bestMatch = kvp.Key;
                    longestMatchLength = varValue.Length;
                }
            }
            
            // Replace with variable reference if found
            string shrunkValue;
            if (bestMatch != null)
            {
                var varValue = allVars[bestMatch].TrimEnd('\\');
                var remainder = expandedValue.Substring(varValue.Length).TrimStart('\\');
                shrunkValue = string.IsNullOrEmpty(remainder) 
                    ? $"%{bestMatch}%" 
                    : $"%{bestMatch}%\\{remainder}";
            }
            else
            {
                shrunkValue = item.Value;
            }
            
            ViewModel.StatusMessage = $"Shrinking {item.Name}";
            
            if (isSystemVariable)
                service.SetSystemVariable(item.Name, shrunkValue);
            else
                service.SetUserVariable(item.Name, shrunkValue);
            
            ViewModel.RefreshVariables();
            UpdateStatusBar();
            ViewModel.StatusMessage = $"Shrunk value in {item.Name}";
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Error shrinking: {ex.Message}";
            UpdateStatusBar();
        }
    }

    private async void ExpandValueButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as MenuFlyoutItem)?.DataContext is not EnvVariableItem item)
            return;

        // Determine if this is a user or system variable
        bool isSystemVariable = ViewModel.SystemVariables.Contains(item);

        // Check admin permissions for system variables
        if (isSystemVariable && !ViewModel.IsAdmin)
        {
            ViewModel.StatusMessage = "Administrator privileges required to modify system variables";
            UpdateStatusBar();
            return;
        }

        try
        {
            var service = new Services.EnvironmentVariableService();
            
            // Expand the value
            var expandedValue = Environment.ExpandEnvironmentVariables(item.Value);
            
            ViewModel.StatusMessage = $"Expanding {item.Name}";
            
            if (isSystemVariable)
                service.SetSystemVariable(item.Name, expandedValue);
            else
                service.SetUserVariable(item.Name, expandedValue);
            
            ViewModel.RefreshVariables();
            UpdateStatusBar();
            ViewModel.StatusMessage = $"Expanded value in {item.Name}";
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Error expanding: {ex.Message}";
            UpdateStatusBar();
        }
    }

    private async void DeleteChildButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not EnvVariableItem childItem)
            return;

        if (!childItem.IsChild)
            return;

        // Find parent variable
        EnvVariableItem? parentVariable = null;
        bool isSystemVariable = false;

        foreach (var userVar in ViewModel.UserVariables)
        {
            if (userVar.Children.Contains(childItem))
            {
                parentVariable = userVar;
                isSystemVariable = false;
                break;
            }
        }

        if (parentVariable == null)
        {
            foreach (var sysVar in ViewModel.SystemVariables)
            {
                if (sysVar.Children.Contains(childItem))
                {
                    parentVariable = sysVar;
                    isSystemVariable = true;
                    break;
                }
            }
        }

        if (parentVariable == null) return;

        // Check admin permissions for system variables
        if (isSystemVariable && !ViewModel.IsAdmin)
        {
            ViewModel.StatusMessage = "Administrator privileges required to modify system variables";
            UpdateStatusBar();
            return;
        }

        // Confirm deletion
        var confirmDialog = new ContentDialog
        {
            Title = "Confirm Delete",
            Content = $"Are you sure you want to delete this path entry?\n\n{childItem.DisplayName}",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.Content.XamlRoot
        };

        var result = await confirmDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            try
            {
                var service = new Services.EnvironmentVariableService();

                // Remove the child and reconstruct the parent PATH variable
                var paths = parentVariable.Children.Where(c => c != childItem).Select(c => c.DisplayName).ToList();
                string newValue = string.Join(";", paths);

                ViewModel.StatusMessage = $"Removing path entry from {parentVariable.Name}";

                if (isSystemVariable)
                    service.SetSystemVariable(parentVariable.Name, newValue);
                else
                    service.SetUserVariable(parentVariable.Name, newValue);

                ViewModel.RefreshVariables();
                UpdateStatusBar();
                ViewModel.StatusMessage = $"Removed path entry from {parentVariable.Name}";
            }
            catch (Exception ex)
            {
                ViewModel.StatusMessage = $"Error removing path entry: {ex.Message}";
                UpdateStatusBar();
            }
        }
    }

    private async Task DeleteChildEntry(EnvVariableItem childItem, bool isSystemVariable)
    {
        // Find parent variable
        EnvVariableItem? parentVariable = null;

        foreach (var userVar in ViewModel.UserVariables)
        {
            if (userVar.Children.Contains(childItem))
            {
                parentVariable = userVar;
                break;
            }
        }

        if (parentVariable == null)
        {
            foreach (var sysVar in ViewModel.SystemVariables)
            {
                if (sysVar.Children.Contains(childItem))
                {
                    parentVariable = sysVar;
                    break;
                }
            }
        }

        if (parentVariable == null) return;

        // Check admin permissions for system variables
        if (isSystemVariable && !ViewModel.IsAdmin)
        {
            ViewModel.StatusMessage = "Administrator privileges required to modify system variables";
            UpdateStatusBar();
            return;
        }

        // Confirm deletion
        var confirmDialog = new ContentDialog
        {
            Title = "Confirm Delete",
            Content = $"Are you sure you want to delete this path entry?\n\n{childItem.DisplayName}",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.Content.XamlRoot
        };

        var result = await confirmDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            try
            {
                var service = new Services.EnvironmentVariableService();

                // Remove the child and reconstruct the parent PATH variable
                var paths = parentVariable.Children.Where(c => c != childItem).Select(c => c.DisplayName).ToList();
                string newValue = string.Join(";", paths);

                ViewModel.StatusMessage = $"Removing path entry from {parentVariable.Name}";

                if (isSystemVariable)
                    service.SetSystemVariable(parentVariable.Name, newValue);
                else
                    service.SetUserVariable(parentVariable.Name, newValue);

                ViewModel.RefreshVariables();
                UpdateStatusBar();
                ViewModel.StatusMessage = $"Removed path entry from {parentVariable.Name}";
            }
            catch (Exception ex)
            {
                ViewModel.StatusMessage = $"Error removing path entry: {ex.Message}";
                UpdateStatusBar();
            }
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
        if (sender is StackPanel stackPanel && stackPanel.DataContext is EnvVariableItem item)
        {
            // All child items (leaf entries) span all columns
            if (item.IsChild)
            {
                // Find the parent Grid and make it span all columns
                if (stackPanel.Parent is Grid parentGrid)
                {
                    Grid.SetColumnSpan(parentGrid, 3);
                    Canvas.SetZIndex(parentGrid, 1);  // Ensure it's on top
                }
            }
        }
    }
}
