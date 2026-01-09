using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using EnvFlow.Dialogs;
using EnvFlow.Helpers;
using EnvFlow.Models;
using EnvFlow.Services;
using EnvFlow.ViewModels;

using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

using Windows.UI;

namespace EnvFlow;

public sealed partial class MainWindow : Window
{
    public MainWindowViewModel ViewModel { get; }
    private readonly EnvVarService _envService;
    private EnvVarItem? _currentlyEditingItem;
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

                _envService.SetVariable(EnvironmentVariableTarget.User, dialog.VariableName, dialog.VariableValue);
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

                _envService.SetVariable(EnvironmentVariableTarget.Machine, dialog.VariableName, dialog.VariableValue);
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
        ViewModel.SelectedUserVariable.EditValue = ViewModel.SelectedUserVariable.IsEntry
            ? ViewModel.SelectedUserVariable.Name
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
        ViewModel.SelectedSystemVariable.EditValue = ViewModel.SelectedSystemVariable.IsEntry
            ? ViewModel.SelectedSystemVariable.Name
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
        if (ViewModel.SelectedUserVariable.IsEntry)
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
        if (ViewModel.SelectedSystemVariable.IsEntry)
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

    private async void EditItemButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not EnvVarItem item)
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

        // Open VariableEditorDialog for editing
        if (item.IsEntry)
        {
            // For child items, open dialog in path entry mode
            var dialog = new Dialogs.VariableEditorDialog
            {
                XamlRoot = this.Content.XamlRoot
            };

            // Find parent variable
            EnvVarItem? parentItem = null;
            foreach (var userVar in ViewModel.UserVariables)
            {
                if (userVar.Children.Contains(item))
                {
                    parentItem = userVar;
                    break;
                }
            }

            if (parentItem == null)
            {
                foreach (var sysVar in ViewModel.SystemVariables)
                {
                    if (sysVar.Children.Contains(item))
                    {
                        parentItem = sysVar;
                        break;
                    }
                }
            }

            if (parentItem == null) return;

            dialog.ConfigureForEntry(parentItem.Name, isEditMode: true);
            dialog.VariableValue = item.Name;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(dialog.VariableValue))
            {
                try
                {


                    // Update the path in the parent's value
                    var paths = parentItem.Children.Select(c => c == item ? dialog.VariableValue.Trim() : c.Name).ToList();
                    string newValue = string.Join(";", paths);

                    ViewModel.StatusMessage = $"Updating path entry in {parentItem.Name}";

                    if (isSystemVariable)
                        _envService.SetVariable(EnvironmentVariableTarget.Machine, parentItem.Name, newValue);
                    else
                        _envService.SetVariable(EnvironmentVariableTarget.User, parentItem.Name, newValue);

                    ViewModel.RefreshVariables();
                    UpdateStatusBar();
                    ViewModel.StatusMessage = $"Updated path entry in {parentItem.Name}";
                }
                catch (Exception ex)
                {
                    ViewModel.StatusMessage = $"Error updating path entry: {ex.Message}";
                    UpdateStatusBar();
                }
            }
        }
        else
        {
            // For parent/single value items, open dialog in normal mode
            var dialog = new Dialogs.VariableEditorDialog
            {
                XamlRoot = this.Content.XamlRoot,
                Title = "Edit Variable",
                IsEditMode = true
            };

            dialog.VariableName = item.Name;
            dialog.VariableValue = item.Value;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                try
                {


                    ViewModel.StatusMessage = $"Updating {item.Name}";

                    if (isSystemVariable)
                        _envService.SetVariable(EnvironmentVariableTarget.Machine, item.Name, dialog.VariableValue);
                    else
                        _envService.SetVariable(EnvironmentVariableTarget.User, item.Name, dialog.VariableValue);

                    ViewModel.RefreshVariables();
                    UpdateStatusBar();
                    ViewModel.StatusMessage = $"Updated {item.Name}";
                }
                catch (Exception ex)
                {
                    ViewModel.StatusMessage = $"Error updating variable: {ex.Message}";
                    UpdateStatusBar();
                }
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

    private async void DeleteItemButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not EnvVarItem item)
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
        if (item.IsEntry)
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
        if ((sender as FrameworkElement)?.DataContext is not EnvVarItem item)
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
        item.EditValue = item.IsEntry ? item.Name : item.Value;
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
        if (sender is not TextBox textBox || textBox.DataContext is not EnvVarItem item)
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
        if (sender is TextBox textBox && textBox.DataContext is EnvVarItem item)
        {
            SaveInlineEdit(item);
        }
    }

    private void SaveInlineEdit(EnvVarItem item)
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
        string originalValue = item.IsEntry ? item.Name : item.Value;
        if (item.EditValue == originalValue)
            return;

        // Determine if this is a user or system variable (or child of one)
        bool isSystemVariable = ViewModel.SystemVariables.Contains(item);
        EnvVarItem? parentVariable = null;

        if (item.IsEntry)
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


            if (item.IsEntry && parentVariable != null)
            {
                // Update the child and reconstruct the parent PATH variable
                var paths = parentVariable.Children.Select(c => c == item ? item.EditValue : c.Name).ToList();
                string newValue = string.Join(";", paths);

                ViewModel.StatusMessage = $"Updating {(isSystemVariable ? "system" : "user")} path entry in {parentVariable.Name}";

                if (isSystemVariable)
                    _envService.SetVariable(EnvironmentVariableTarget.Machine, parentVariable.Name, newValue);
                else
                    _envService.SetVariable(EnvironmentVariableTarget.User, parentVariable.Name, newValue);
            }
            else
            {
                ViewModel.StatusMessage = $"Updating {(isSystemVariable ? "system" : "user")} variable: {item.Name}";

                if (isSystemVariable)
                    _envService.SetVariable(EnvironmentVariableTarget.Machine, item.Name, item.EditValue);
                else
                    _envService.SetVariable(EnvironmentVariableTarget.User, item.Name, item.EditValue);
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

    private bool IsInUserTreeView(EnvVarItem item)
    {
        // Check if the item exists in the user variables collection
        return ViewModel.UserVariables.Any(v => v == item || v.Children.Contains(item));
    }

    private EnvVarItem? GetParentVariable(EnvVarItem childItem, bool isUserVariable)
    {
        var collection = isUserVariable ? ViewModel.UserVariables : ViewModel.SystemVariables;
        return collection.FirstOrDefault(v => v.Children.Contains(childItem));
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

    private async void DeleteChildButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not EnvVarItem childItem)
            return;

        if (!childItem.IsEntry)
            return;

        // Find parent variable
        EnvVarItem? parentVariable = null;
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
            Content = $"Are you sure you want to delete this entry?\n\n{childItem.Name}",
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


                // Remove the child and reconstruct the parent PATH variable
                var paths = parentVariable.Children.Where(c => c != childItem).Select(c => c.Name).ToList();
                string newValue = string.Join(";", paths);

                ViewModel.StatusMessage = $"Removing path entry from {parentVariable.Name}";

                if (isSystemVariable)
                    _envService.SetVariable(EnvironmentVariableTarget.Machine, parentVariable.Name, newValue);
                else
                    _envService.SetVariable(EnvironmentVariableTarget.User, parentVariable.Name, newValue);

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

    private async Task DeleteChildEntry(EnvVarItem childItem, bool isSystemVariable)
    {
        // Find parent variable
        EnvVarItem? parentVariable = null;

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
            Content = $"Are you sure you want to delete this entry?\n\n{childItem.Name}",
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


                // Remove the child and reconstruct the parent PATH variable
                var paths = parentVariable.Children.Where(c => c != childItem).Select(c => c.Name).ToList();
                string newValue = string.Join(";", paths);

                ViewModel.StatusMessage = $"Removing path entry from {parentVariable.Name}";

                if (isSystemVariable)
                    _envService.SetVariable(EnvironmentVariableTarget.Machine, parentVariable.Name, newValue);
                else
                    _envService.SetVariable(EnvironmentVariableTarget.User, parentVariable.Name, newValue);

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
        if (sender is StackPanel stackPanel && stackPanel.DataContext is EnvVarItem item)
        {
            // All child items (leaf entries) span all columns
            if (item.IsEntry)
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

    private void HoverButtons_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not StackPanel panel || panel.Tag?.ToString() != "System")
            return;

        // Only gray out and disable buttons if not admin (for system variables)
        if (!ViewModel.IsAdmin)
        {
            var grayBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 160, 160, 160));

            foreach (var child in panel.Children)
            {
                if (child is Button button && button.Content is FontIcon icon)
                {
                    icon.Foreground = grayBrush;
                    button.IsEnabled = false;
                }
            }
        }
    }

    private void MoreOptionsButton_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        // Check if this is a system variable and disable if not admin
        if (button.DataContext is EnvVarItem item && item.IsSystemVariable && !ViewModel.IsAdmin)
        {
            button.IsEnabled = false;
        }
    }
}
