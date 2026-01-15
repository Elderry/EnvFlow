using System;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;

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

    public bool IsEntryMode { get; set; }

    public VariableEditorDialog()
    {
        InitializeComponent();
        UpdatePrimaryButtonState();
    }

    public void ConfigureForEntry(string parentVariableName, bool isEditMode = false)
    {
        IsEntryMode = true;
        IsEditMode = isEditMode;
        Title = isEditMode ? $"Edit Entry in [{parentVariableName}]" : $"Add Entry to [{parentVariableName}]";
        PrimaryButtonText = isEditMode ? "Save" : "Add";
        VariableNameLabel.Visibility = Visibility.Collapsed;
        VariableNameTextBox.Visibility = Visibility.Collapsed;
        VariableValueTextBox.PlaceholderText = "Enter entry value";
    }

    private async void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        Button button = (Button)sender;
        FolderPicker folderPicker = new(button.XamlRoot.ContentIslandEnvironment.AppWindowId);

        PickFolderResult folder = await folderPicker.PickSingleFolderAsync();
        if (folder != null)
        {
            VariableValueTextBox.Text = folder.Path;
        }
    }

    private async void BrowseFile_Click(object sender, RoutedEventArgs e)
    {
        Button button = (Button)sender;
        FileOpenPicker filePicker = new(button.XamlRoot.ContentIslandEnvironment.AppWindowId);
        filePicker.FileTypeFilter.Add("*");

        PickFileResult file = await filePicker.PickSingleFileAsync();
        if (file != null)
        {
            VariableValueTextBox.Text = file.Path;
        }
    }

    private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdatePrimaryButtonState();
    }

    private void UpdatePrimaryButtonState()
    {
        if (IsEntryMode)
        {
            bool isEmpty = string.IsNullOrWhiteSpace(VariableValueTextBox.Text);
            IsPrimaryButtonEnabled = !isEmpty;
            
            if (isEmpty)
            {
                ErrorTextBlock.Text = "Path cannot be empty.";
                ErrorTextBlock.Visibility = Visibility.Visible;
            }
            else
            {
                ErrorTextBlock.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            bool isNameEmpty = string.IsNullOrWhiteSpace(VariableNameTextBox.Text);
            bool hasInvalidChar = !isNameEmpty && VariableNameTextBox.Text.Contains('=');
            
            IsPrimaryButtonEnabled = !isNameEmpty && !hasInvalidChar;
            
            if (isNameEmpty)
            {
                ErrorTextBlock.Text = "Variable name cannot be empty.";
                ErrorTextBlock.Visibility = Visibility.Visible;
            }
            else if (hasInvalidChar)
            {
                ErrorTextBlock.Text = "Variable name cannot contain '=' character.";
                ErrorTextBlock.Visibility = Visibility.Visible;
            }
            else
            {
                ErrorTextBlock.Visibility = Visibility.Collapsed;
            }
        }
    }
}
