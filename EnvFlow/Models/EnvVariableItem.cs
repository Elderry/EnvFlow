using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace EnvFlow;

public class EnvVariableItem : INotifyPropertyChanged
{
    private bool _isEditing;
    private string _editValue = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Icon { get; set; } = "\uE838"; // Default folder icon
    public Brush IconColor { get; set; } = new SolidColorBrush(Colors.Gray);
    public ObservableCollection<EnvVariableItem> Children { get; set; } = new();
    public bool IsPathEntry { get; set; }
    public bool IsValid { get; set; } = true;
    public Visibility ValueVisibility { get; set; } = Visibility.Collapsed;
    public bool IsChild => IsPathEntry; // Path entries are children of the parent variable

    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            _isEditing = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayVisibility));
            OnPropertyChanged(nameof(EditVisibility));
            OnPropertyChanged(nameof(ValueDisplayVisibility));
            OnPropertyChanged(nameof(DisplayNameVisibility));
            OnPropertyChanged(nameof(AddChildButtonVisibility));
        }
    }

    public string EditValue
    {
        get => _editValue;
        set
        {
            _editValue = value;
            OnPropertyChanged();
        }
    }

    public Visibility DisplayVisibility => IsEditing ? Visibility.Collapsed : Visibility.Visible;
    public Visibility EditVisibility => IsEditing ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ValueDisplayVisibility => (IsEditing || ValueVisibility == Visibility.Collapsed || IsChild) 
        ? Visibility.Collapsed 
        : Visibility.Visible;
    public Visibility DisplayNameVisibility => (IsChild && IsEditing) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility AddChildButtonVisibility => (!IsChild && Children.Count > 0 && !IsEditing) 
        ? Visibility.Visible 
        : Visibility.Collapsed;

    public EnvVariableItem()
    {
    }

    public EnvVariableItem(string name, string value, bool isPathLike = false)
    {
        Name = name;
        Value = value;
        DisplayName = name;
        IsPathEntry = false;

        // Check if value looks like a file system path
        bool isSinglePath = !string.IsNullOrEmpty(value) && 
                           !value.Contains(';') &&
                           (value.Contains('\\') || value.Contains(':'));

        // PATHEXT is special - it contains extensions, not paths
        bool isPathExt = name.Equals("PATHEXT", StringComparison.OrdinalIgnoreCase);

        // Variable-level item
        if (isPathLike && !isPathExt && !string.IsNullOrEmpty(value))
        {
            Icon = "\uE8B7"; // Folder icon for PATH variables
            IconColor = new SolidColorBrush(Colors.Orange);
            
            // Parse path entries as children
            var entries = value.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var entry in entries)
            {
                if (!string.IsNullOrWhiteSpace(entry))
                {
                    Children.Add(CreatePathEntry(entry.Trim()));
                }
            }
            
            ValueVisibility = Visibility.Collapsed;
            
            // Notify AddChildButtonVisibility to update
            OnPropertyChanged(nameof(AddChildButtonVisibility));
        }
        else if (isPathExt && !string.IsNullOrEmpty(value))
        {
            // PATHEXT contains file extensions, show as expandable list
            Icon = "\uE8E9"; // List icon
            IconColor = new SolidColorBrush(Colors.Orange);
            
            var extensions = value.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var ext in extensions)
            {
                if (!string.IsNullOrWhiteSpace(ext))
                {
                    Children.Add(new EnvVariableItem
                    {
                        Name = ext.Trim(),
                        DisplayName = ext.Trim(),
                        Value = ext.Trim(),
                        Icon = "\uE8A5", // Document icon
                        IconColor = new SolidColorBrush(Colors.MediumPurple),
                        IsPathEntry = false,
                        ValueVisibility = Visibility.Collapsed
                    });
                }
            }
        }
        else if (isSinglePath)
        {
            // Single path value (like OneDrive, TEMP, etc.)
            bool exists = System.IO.Directory.Exists(value) || System.IO.File.Exists(value);
            Icon = "\uE8B7"; // Folder icon
            IconColor = new SolidColorBrush(exists ? Colors.MediumSeaGreen : Colors.Crimson);
            ValueVisibility = Visibility.Visible;
        }
        else
        {
            Icon = "\uE70F"; // Tag/Label icon for regular variables
            IconColor = new SolidColorBrush(Colors.SkyBlue);
            ValueVisibility = Visibility.Visible;
        }
    }

    private EnvVariableItem CreatePathEntry(string path)
    {
        bool exists = System.IO.Directory.Exists(path) || System.IO.File.Exists(path);
        
        return new EnvVariableItem
        {
            Name = path,
            Value = path,
            DisplayName = path,
            IsPathEntry = true,
            IsValid = exists,
            Icon = exists ? "\uE8B7" : "\uE7BA", // Folder icon or error icon
            IconColor = new SolidColorBrush(exists ? Colors.LimeGreen : Colors.Crimson),
            ValueVisibility = Visibility.Collapsed
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
