using Microsoft.UI.Xaml.Controls;

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

    public VariableEditorDialog()
    {
        InitializeComponent();
        UpdatePrimaryButtonState();
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // Validate input
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
        IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(VariableNameTextBox.Text);
    }
}
