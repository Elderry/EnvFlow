using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using EnvFlow.ViewModels;
using EnvFlow.Helpers;
using EnvFlow.Dialogs;

namespace EnvFlow;

public sealed partial class MainWindow : Window
{
    public MainWindowViewModel ViewModel { get; }
    private EnvVariableItem? _currentlyEditingItem;

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
        if (ViewModel.SelectedUserVariable == null || ViewModel.SelectedUserVariable.IsChild)
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
        ViewModel.SelectedUserVariable.EditValue = ViewModel.SelectedUserVariable.Value;
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

        if (ViewModel.SelectedSystemVariable == null || ViewModel.SelectedSystemVariable.IsChild)
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
        ViewModel.SelectedSystemVariable.EditValue = ViewModel.SelectedSystemVariable.Value;
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

    private async void TreeViewItem_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        // Get the data context (EnvVariableItem) from the tapped element
        if ((sender as FrameworkElement)?.DataContext is not EnvVariableItem item)
            return;

        // Prevent event from bubbling up
        e.Handled = true;

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

        // Show dialog to add new path entry
        var dialog = new ContentDialog
        {
            Title = $"Add Path Entry to {parentItem.Name}",
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.Content.XamlRoot
        };

        var textBox = new TextBox
        {
            PlaceholderText = "Enter new path (e.g., C:\\Program Files\\MyApp)",
            MinWidth = 400
        };

        dialog.Content = textBox;

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text))
        {
            try
            {
                var service = new Services.EnvironmentVariableService();
                
                // Add the new path to the existing paths
                var existingPaths = parentItem.Children.Select(c => c.DisplayName).ToList();
                existingPaths.Add(textBox.Text.Trim());
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
}
