using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using EnvFlow.Constants;
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
    public string Icon { get; set; } = AppIcons.Library;
    public Brush IconColor { get; set; } = new SolidColorBrush(Colors.Gray);
    public ObservableCollection<EnvVariableItem> Children { get; set; } = new();
    public bool IsPathEntry { get; set; }
    public bool IsValid { get; set; } = true;
    public bool IsReadOnly { get; set; } = false; // For volatile environment variables
    public bool IsExpanded { get; set; } = false; // Track expand/collapse state
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
            OnPropertyChanged(nameof(ChildEditVisibility));
            OnPropertyChanged(nameof(ValueDisplayVisibility));
            OnPropertyChanged(nameof(DisplayNameVisibility));
            OnPropertyChanged(nameof(AddChildButtonVisibility));
            OnPropertyChanged(nameof(NormalizeButtonVisibility));
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
    public Visibility EditVisibility => (IsEditing && !IsChild) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ChildEditVisibility => (IsEditing && IsChild) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ValueDisplayVisibility => (IsEditing || ValueVisibility == Visibility.Collapsed || IsChild) 
        ? Visibility.Collapsed 
        : Visibility.Visible;
    public Visibility DisplayNameVisibility => (IsChild && IsEditing) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ColumnSeparatorVisibility => (IsChild || Children.Count > 0) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility IsChildVisibility => IsChild ? Visibility.Visible : Visibility.Collapsed;
    public Visibility AddChildButtonVisibility => (!IsChild && Children.Count > 0 && !IsEditing) 
        ? Visibility.Visible 
        : Visibility.Collapsed;
    public Visibility NormalizeButtonVisibility => (!IsChild && Children.Count > 0 && !IsEditing && !IsReadOnly) 
        ? Visibility.Visible 
        : Visibility.Collapsed;
    
    public Visibility HoverButtonsVisibility => !IsReadOnly 
        ? Visibility.Visible 
        : Visibility.Collapsed;
    
    public Visibility EditButtonVisibility => (!IsChild && Children.Count > 0) 
        ? Visibility.Collapsed 
        : Visibility.Visible;
    
    public Visibility MoreOptionsVisibility => (!IsReadOnly && (IsChild || Children.Count == 0)) 
        ? Visibility.Visible 
        : Visibility.Collapsed;
    
    public bool IsComposite => !IsChild && Children.Count > 0;

    public EnvVariableItem()
    {
    }

    public EnvVariableItem(string name, string value, bool isReadOnly = false)
    {
        Name = name;
        Value = value;
        DisplayName = name;
        IsPathEntry = false;
        IsReadOnly = isReadOnly;

        bool isPathLike = value.Contains('\\') || value.Contains('/') || value.Contains(':');
        bool isMultiValue = value.Contains(';');

        // Variable-level item
        if (isMultiValue)
        {
            Icon = isPathLike ? AppIcons.Library : AppIcons.DialShape1;
            IconColor = new SolidColorBrush(Colors.Orange);

            // Parse path entries as children
            var entries = value.Split(';');
            foreach (var entry in entries)
            {
                Children.Add(CreateVarEntry(entry));
            }

            ValueVisibility = Visibility.Collapsed;

            // Notify AddChildButtonVisibility to update
            OnPropertyChanged(nameof(AddChildButtonVisibility));
        }
        else if (isPathLike)
        {
            // Single path value (like OneDrive, TEMP, etc.)
            if (IsReadOnly)
            {
                // Volatile variables - gray color, no validation
                Icon = AppIcons.Folder;
                IconColor = new SolidColorBrush(Colors.Gray);
            }
            else
            {
                var expandedValue = Environment.ExpandEnvironmentVariables(value);
                bool isFolder = Directory.Exists(expandedValue);
                bool isFile = File.Exists(expandedValue);
				bool exists =  isFolder || isFile;
                Icon = isFolder ? AppIcons.Folder : isFile ? AppIcons.File : AppIcons.Error;
                IconColor = new SolidColorBrush(exists ? Colors.MediumSeaGreen : Colors.Crimson);
            }
            ValueVisibility = Visibility.Visible;
        }
        else
        {
            Icon = AppIcons.Tag;
            IconColor = new SolidColorBrush(IsReadOnly ? Colors.Gray : Colors.DeepSkyBlue);
            ValueVisibility = Visibility.Visible;
        }
    }

    private EnvVariableItem CreateVarEntry(string value)
    {
        // Expand for validation purposes only
        var expanded = Environment.ExpandEnvironmentVariables(value);
        bool isPathLike = expanded.Contains('\\') || expanded.Contains('/') || expanded.Contains(':');
        bool isFolder = Directory.Exists(expanded);
        bool isFile = File.Exists(expanded);
        bool exists = isFolder || isFile;

        return new EnvVariableItem
        {
            Name = value,
            Value = value,
            DisplayName = value,
            IsPathEntry = true,
            IsValid = exists,
            Icon = isFolder ? AppIcons.Folder : isFile ? AppIcons.File : isPathLike ? AppIcons.Error : AppIcons.Tag,
            IconColor = new SolidColorBrush(exists ? Colors.MediumSeaGreen : isPathLike ? Colors.Crimson : Colors.DeepSkyBlue),
            ValueVisibility = Visibility.Collapsed
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
