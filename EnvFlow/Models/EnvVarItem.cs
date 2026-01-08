using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

using EnvFlow.Constants;
using EnvFlow.Helpers;

using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace EnvFlow.Models;

public partial class EnvVarItem : INotifyPropertyChanged
{
    private bool _isEditing;
    private string _editValue = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;

    public string Icon { get; set; } = AppIcons.Tag;
    public Brush IconColor { get; set; } = new SolidColorBrush(Colors.Gray);
    public ObservableCollection<EnvVarItem> Children { get; set; } = [];
    public bool IsEntry { get; set; }
    public bool IsValid { get; set; } = true;
    public bool IsReadOnly { get; set; } = false; // For volatile environment variables
    public bool IsSystemVariable { get; set; } = false; // Track if this is a system variable
    public bool IsExpanded { get; set; } = false; // Track expand/collapse state

    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            _isEditing = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(NameVisibility));
            OnPropertyChanged(nameof(EditVisibility));
            OnPropertyChanged(nameof(ChildEditVisibility));
            OnPropertyChanged(nameof(ValueVisibility));
            OnPropertyChanged(nameof(AddChildButtonVisibility));
            OnPropertyChanged(nameof(SortMenuVisibility));
            OnPropertyChanged(nameof(SortMenuVisibility));
            OnPropertyChanged(nameof(MoreOptionsButtonVisibility));
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

    public bool IsLimitAccess => IsSystemVariable && !AdminHelper.IsAdmin();
    public bool IsComposite => Children.Count > 0;

    // Tree view item visibility
    public Visibility NameVisibility => (IsEditing && IsEntry) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ValueVisibility => (IsEditing || IsEntry || IsComposite) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility EditVisibility => (IsEditing && !IsEntry) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ChildEditVisibility => (IsEditing && IsEntry) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ColumnSeparatorVisibility => (IsEntry || IsComposite) ? Visibility.Collapsed : Visibility.Visible;

    // Hover button visibility
    public Visibility AddChildButtonVisibility => !IsReadOnly && IsComposite
        ? Visibility.Visible
        : Visibility.Collapsed;
    public Visibility EditButtonVisibility => !IsReadOnly && !IsComposite
        ? Visibility.Visible
        : Visibility.Collapsed;
    public Visibility DeleteButtonVisibility => !IsReadOnly
        ? Visibility.Visible
        : Visibility.Collapsed;
    public Visibility MoreOptionsButtonVisibility => !IsReadOnly
        ? Visibility.Visible
        : Visibility.Collapsed;

    // Context menu visibility
    public Visibility CopyNameMenuVisibility => !IsEntry
        ? Visibility.Visible
        : Visibility.Collapsed;
    public Visibility AddChildMenuVisibility => !IsReadOnly && IsComposite && !IsLimitAccess
        ? Visibility.Visible
        : Visibility.Collapsed;
    public Visibility EditMenuVisibility => !IsReadOnly && !IsComposite && !IsLimitAccess
        ? Visibility.Visible
        : Visibility.Collapsed;
    public Visibility DeleteMenuVisibility => !IsReadOnly && !IsLimitAccess
        ? Visibility.Visible
        : Visibility.Collapsed;
    public Visibility SortMenuVisibility => !IsReadOnly && IsComposite && !IsLimitAccess
        ? Visibility.Visible
        : Visibility.Collapsed;
    public Visibility ShrinkMenuVisibility => !IsReadOnly && !IsComposite && !IsLimitAccess
        ? Visibility.Visible
        : Visibility.Collapsed;
    public Visibility ShrinkEntriesMenuVisibility => !IsReadOnly && IsComposite && !IsLimitAccess
        ? Visibility.Visible
        : Visibility.Collapsed;
    public Visibility ExpandMenuVisibility => !IsReadOnly && !IsComposite && !IsLimitAccess
        ? Visibility.Visible
        : Visibility.Collapsed;
    public Visibility ExpandEntriesMenuVisibility => !IsReadOnly && IsComposite && !IsLimitAccess
        ? Visibility.Visible
        : Visibility.Collapsed;

    public EnvVarItem()
    {
    }

    public EnvVarItem(string name, string value, bool isReadOnly = false, bool isSystemVariable = false)
    {
        Name = name;
        Value = value;
        IsEntry = false;
        IsReadOnly = isReadOnly;
        IsSystemVariable = isSystemVariable;

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
                bool exists = isFolder || isFile;
                Icon = isFolder ? AppIcons.Folder : isFile ? AppIcons.File : AppIcons.Error;
                IconColor = new SolidColorBrush(exists ? Colors.MediumSeaGreen : Colors.Crimson);
            }
        }
        else
        {
            Icon = AppIcons.Tag;
            IconColor = new SolidColorBrush(IsReadOnly ? Colors.Gray : Colors.DeepSkyBlue);
        }
    }

    private EnvVarItem CreateVarEntry(string value)
    {
        // Expand for validation purposes only
        string expanded = Environment.ExpandEnvironmentVariables(value);
        bool isPathLike = expanded.Contains('\\') || expanded.Contains('/') || expanded.Contains(':');
        bool isFolder = Directory.Exists(expanded);
        bool isFile = File.Exists(expanded);
        bool exists = isFolder || isFile;

        return new EnvVarItem
        {
            Name = value,
            Value = value,
            IsEntry = true,
            IsValid = exists,
            Icon = isFolder ? AppIcons.Folder : isFile ? AppIcons.File : isPathLike ? AppIcons.Error : AppIcons.Tag,
            IconColor = new SolidColorBrush(exists ? Colors.MediumSeaGreen : isPathLike ? Colors.Crimson : Colors.DeepSkyBlue),
            IsSystemVariable = IsSystemVariable
        };
    }

    public void UpdateChildrenProperties()
    {
        foreach (var child in Children)
        {
            child.IsSystemVariable = IsSystemVariable;
            child.IsReadOnly = IsReadOnly;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
