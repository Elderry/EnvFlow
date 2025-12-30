using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using EnvFlow.ViewModels;
using EnvFlow.Helpers;
using EnvFlow.Dialogs;

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

    private async void EditUserVariableButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedUserVariable == null || ViewModel.SelectedUserVariable.IsChild)
        {
            ViewModel.StatusMessage = "Please select a variable to edit";
            UpdateStatusBar();
            return;
        }

        var dialog = new VariableEditorDialog
        {
            Title = "Edit User Variable",
            VariableName = ViewModel.SelectedUserVariable.Name,
            VariableValue = ViewModel.SelectedUserVariable.Value,
            IsEditMode = true,
            XamlRoot = this.Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            try
            {
                ViewModel.StatusMessage = $"Updating user variable: {dialog.VariableName}";
                var service = new Services.EnvironmentVariableService();
                service.SetUserVariable(dialog.VariableName, dialog.VariableValue);
                ViewModel.RefreshVariables();
                UpdateStatusBar();
                ViewModel.StatusMessage = $"Updated user variable: {dialog.VariableName}";
            }
            catch (Exception ex)
            {
                ViewModel.StatusMessage = $"Error updating variable: {ex.Message}";
                UpdateStatusBar();
            }
        }
    }

    private async void EditSystemVariableButton_Click(object sender, RoutedEventArgs e)
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

        var dialog = new VariableEditorDialog
        {
            Title = "Edit System Variable",
            VariableName = ViewModel.SelectedSystemVariable.Name,
            VariableValue = ViewModel.SelectedSystemVariable.Value,
            IsEditMode = true,
            XamlRoot = this.Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            try
            {
                ViewModel.StatusMessage = $"Updating system variable: {dialog.VariableName}";
                var service = new Services.EnvironmentVariableService();
                service.SetSystemVariable(dialog.VariableName, dialog.VariableValue);
                ViewModel.RefreshVariables();
                UpdateStatusBar();
                ViewModel.StatusMessage = $"Updated system variable: {dialog.VariableName}";
            }
            catch (Exception ex)
            {
                ViewModel.StatusMessage = $"Error updating variable: {ex.Message}";
                UpdateStatusBar();
            }
        }
    }

    private async void DeleteUserVariableButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedUserVariable == null || ViewModel.SelectedUserVariable.IsChild)
        {
            ViewModel.StatusMessage = "Please select a variable to delete";
            UpdateStatusBar();
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

        if (ViewModel.SelectedSystemVariable == null || ViewModel.SelectedSystemVariable.IsChild)
        {
            ViewModel.StatusMessage = "Please select a variable to delete";
            UpdateStatusBar();
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
}
