using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;

namespace EnvFlow.Dialogs;

public sealed partial class VariableEditorDialog : ContentDialog
{
    public string VariableName
    {
        get => VariableNameTextBox.Text;
        set => VariableNameTextBox.Text = value;
    }

    public string VariableValue
    {
        get => VariableValueTextBox.Text;
        set => VariableValueTextBox.Text = value;
    }

    public bool IsEditMode { get; set; }
    
    public bool IsPathEntryMode { get; set; }

    public VariableEditorDialog()
    {
        InitializeComponent();
        UpdatePrimaryButtonState();
    }
    
    public void ConfigureForPathEntry(string parentVariableName, bool isEditMode = false)
    {
        IsPathEntryMode = true;
        IsEditMode = isEditMode;
        Title = isEditMode ? $"Edit Variable Entry in {parentVariableName}" : $"Add Variable Entry to {parentVariableName}";
        PrimaryButtonText = isEditMode ? "Save" : "Add";
        VariableNameLabel.Visibility = Visibility.Collapsed;
        VariableNameTextBox.Visibility = Visibility.Collapsed;
        VariableValueTextBox.PlaceholderText = "Enter new path (e.g., C:\\Program Files\\MyApp)";
        VariableValueTextBox.MinHeight = 40;
        VariableValueTextBox.MaxHeight = 100;
    }

    private async void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var folderPicker = new Windows.Storage.Pickers.FolderPicker();
        folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
        folderPicker.FileTypeFilter.Add("*");
        
        // Get window handle from App.MainWindow
        if (App.MainWindow != null)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
        }
        
        var folder = await folderPicker.PickSingleFolderAsync();
        if (folder != null)
        {
            VariableValueTextBox.Text = folder.Path;
        }
    }

    private async void BrowseFile_Click(object sender, RoutedEventArgs e)
    {
        var filePicker = new Windows.Storage.Pickers.FileOpenPicker();
        filePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
        filePicker.FileTypeFilter.Add("*");
        
        // Get window handle from App.MainWindow
        if (App.MainWindow != null)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);
        }
        
        var file = await filePicker.PickSingleFileAsync();
        if (file != null)
        {
            VariableValueTextBox.Text = file.Path;
        }
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // Validate input based on mode
        if (IsPathEntryMode)
        {
            // For path entry mode, only validate the value field
            if (string.IsNullOrWhiteSpace(VariableValueTextBox.Text))
            {
                ErrorTextBlock.Text = "Path cannot be empty.";
                ErrorTextBlock.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                args.Cancel = true;
                return;
            }
        }
        else
        {
            // For variable mode, validate the name field
            if (string.IsNullOrWhiteSpace(VariableNameTextBox.Text))
            {
                ErrorTextBlock.Text = "Variable name cannot be empty.";
                ErrorTextBlock.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                args.Cancel = true;
                return;
            }

            if (VariableNameTextBox.Text.Contains('='))
            {
                ErrorTextBlock.Text = "Variable name cannot contain '=' character.";
                ErrorTextBlock.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                args.Cancel = true;
                return;
            }
        }

        ErrorTextBlock.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
    }

    private void ContentDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // User cancelled
    }

    private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdatePrimaryButtonState();
        ErrorTextBlock.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
    }

    private void UpdatePrimaryButtonState()
    {
        if (IsPathEntryMode)
        {
            IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(VariableValueTextBox.Text);
        }
        else
        {
            IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(VariableNameTextBox.Text);
        }
    }
}
