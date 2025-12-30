using System;
using System.Collections.ObjectModel;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace EnvFlow;

public class EnvVariableItem
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Icon { get; set; } = "\uE838"; // Default folder icon
    public Brush IconColor { get; set; } = new SolidColorBrush(Colors.Gray);
    public ObservableCollection<EnvVariableItem> Children { get; set; } = new();
    public bool IsPathEntry { get; set; }
    public bool IsValid { get; set; } = true;
    public Visibility ValueVisibility { get; set; } = Visibility.Collapsed;

    public EnvVariableItem()
    {
    }

    public EnvVariableItem(string name, string value, bool isPathLike = false)
    {
        Name = name;
        Value = value;
        DisplayName = name;
        IsPathEntry = false;

        // Variable-level item
        if (isPathLike && !string.IsNullOrEmpty(value))
        {
            Icon = "\uE8B7"; // Path icon
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
        }
        else
        {
            Icon = "\uE8B9"; // Variable icon
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
            Icon = exists ? "\uE8B7" : "\uE783", // Folder or warning icon
            IconColor = new SolidColorBrush(exists ? Colors.LimeGreen : Colors.OrangeRed),
            ValueVisibility = Visibility.Collapsed
        };
    }
}
