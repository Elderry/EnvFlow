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
            OnPropertyChanged(nameof(SortButtonVisibility));
            OnPropertyChanged(nameof(SortMenuVisibility));
            OnPropertyChanged(nameof(MoreOptionsVisibility));
            OnPropertyChanged(nameof(MoreOptionsMenuButtonVisibility));
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

    // Tree view item visibility
    public Visibility NameVisibility => (IsEditing && IsEntry) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ValueVisibility => (IsEditing || IsEntry || IsComposite) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility EditVisibility => (IsEditing && !IsEntry) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ChildEditVisibility => (IsEditing && IsEntry) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ColumnSeparatorVisibility => (IsEntry || IsComposite) ? Visibility.Collapsed : Visibility.Visible;

    // Hover button visibility
    public Visibility AddChildButtonVisibility => IsComposite ? Visibility.Visible : Visibility.Collapsed;
    
    // Context menu visibility
    public Visibility CopyNameVisibility => !IsEntry ? Visibility.Visible : Visibility.Collapsed;
    public Visibility AddChildMenuVisibility => (IsComposite && !(IsSystemVariable && !AdminHelper.IsAdmin())) 
        ? Visibility.Visible 
        : Visibility.Collapsed;
    
    public Visibility SortButtonVisibility => (!IsEntry && Children.Count > 0 && !IsEditing && !IsReadOnly) 
        ? Visibility.Visible 
        : Visibility.Collapsed;
    
    public Visibility SortMenuVisibility => (!IsEntry && Children.Count > 0 && !IsEditing && !IsReadOnly && !(IsSystemVariable && !AdminHelper.IsAdmin())) 
        ? Visibility.Visible 
        : Visibility.Collapsed;
    
    public Visibility HoverButtonsVisibility => !IsReadOnly 
        ? Visibility.Visible 
        : Visibility.Collapsed;
    
    public Visibility EditButtonVisibility => (!IsEntry && Children.Count > 0) || IsReadOnly
        ? Visibility.Collapsed 
        : Visibility.Visible;
    
    public Visibility EditMenuVisibility => (!IsEntry && Children.Count > 0) || IsReadOnly || (IsSystemVariable && !AdminHelper.IsAdmin())
        ? Visibility.Collapsed 
        : Visibility.Visible;
    
    public Visibility DeleteButtonVisibility => IsReadOnly || (IsSystemVariable && !AdminHelper.IsAdmin())
        ? Visibility.Collapsed
        : Visibility.Visible;
    
    public Visibility MoreOptionsVisibility => (!IsReadOnly && (IsEntry || Children.Count == 0)) 
        ? Visibility.Visible 
        : Visibility.Collapsed;

    public Visibility MoreOptionsMenuButtonVisibility =>
        SortButtonVisibility == Visibility.Visible || MoreOptionsVisibility == Visibility.Visible
            ? Visibility.Visible
            : Visibility.Collapsed;
    
    public bool IsComposite => Children.Count > 0;

    public EnvVarItem()
    {
    }

    public EnvVarItem(string name, string value, bool isReadOnly = false, bool isSystemVariable = false)
    {
        Name = name;
        Value = value;
        IsEntry = false;
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
