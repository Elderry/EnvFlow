using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

using EnvFlow.Helpers;
using EnvFlow.Models;
using EnvFlow.Services;

namespace EnvFlow.ViewModels;

public partial class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly EnvVarService _envService;
    private string _statusMessage = "Ready";
    private int _userVariableCount;
    private int _systemVariableCount;
    private bool _isAdmin;
    private EnvVarItem? _selectedUserVariable;
    private EnvVarItem? _selectedSystemVariable;

    public ObservableCollection<EnvVarItem> UserVariables { get; } = [];
    public ObservableCollection<EnvVarItem> SystemVariables { get; } = [];

    public EnvVarItem? SelectedUserVariable
    {
        get => _selectedUserVariable;
        set
        {
            _selectedUserVariable = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanEditUserVariable));
            OnPropertyChanged(nameof(CanDeleteUserVariable));
        }
    }

    public EnvVarItem? SelectedSystemVariable
    {
        get => _selectedSystemVariable;
        set
        {
            _selectedSystemVariable = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanEditSystemVariable));
            OnPropertyChanged(nameof(CanDeleteSystemVariable));
        }
    }

    public bool CanEditUserVariable => SelectedUserVariable != null && !SelectedUserVariable.IsReadOnly;
    public bool CanDeleteUserVariable => SelectedUserVariable != null && !SelectedUserVariable.IsReadOnly;
    public bool CanEditSystemVariable => IsAdmin && SelectedSystemVariable != null;
    public bool CanDeleteSystemVariable => IsAdmin && SelectedSystemVariable != null;

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public int UserVariableCount
    {
        get => _userVariableCount;
        set
        {
            _userVariableCount = value;
            OnPropertyChanged();
        }
    }

    public int SystemVariableCount
    {
        get => _systemVariableCount;
        set
        {
            _systemVariableCount = value;
            OnPropertyChanged();
        }
    }

    public bool IsAdmin
    {
        get => _isAdmin;
        set
        {
            _isAdmin = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AdminStatusText));
        }
    }

    public string AdminStatusText => IsAdmin ? "Administrator" : "Standard User";

    public MainWindowViewModel(EnvVarService envService)
    {
        _envService = envService;
        IsAdmin = AdminHelper.IsAdmin();
        LoadVariables();
    }

    private void LoadVariables()
    {
        StatusMessage = "Loading environment variables...";

        // Load user variables - update in place to avoid blink
        var userVars = _envService.GetUserVariables();
        UpdateVariableCollection(UserVariables, userVars);
        UserVariableCount = userVars.Count;

        // Load system variables - update in place to avoid blink
        var systemVars = _envService.GetSystemVariables();
        UpdateVariableCollection(SystemVariables, systemVars);
        SystemVariableCount = systemVars.Count;

        StatusMessage = $"Loaded {UserVariableCount} user and {SystemVariableCount} system variables";
    }

    private void UpdateVariableCollection(ObservableCollection<EnvVarItem> collection, List<EnvVarItem> newItems)
    {
        // Save expanded state before clearing
        var expandedStates = collection
            .Where(v => v.IsExpanded)
            .Select(v => v.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Clear and rebuild - simpler and more reliable than in-place updates
        collection.Clear();

        // Add all variables in sorted order
        var sortedItems = newItems.OrderBy(v => v.Name);

        foreach (var item in sortedItems)
        {
            // Restore expanded state
            item.IsExpanded = expandedStates.Contains(item.Name);

            // Update children with parent's properties
            item.UpdateChildrenProperties();

            collection.Add(item);
        }
    }

    public void RefreshVariables()
    {
        LoadVariables();
    }

    public void AddUserVariable()
    {
        // Will be called from UI
    }

    public void AddSystemVariable()
    {
        // Will be called from UI
    }

    public void EditUserVariable()
    {
        if (SelectedUserVariable == null || SelectedUserVariable.IsEntry) return;
        // Will be called from UI
    }

    public void EditSystemVariable()
    {
        if (!IsAdmin || SelectedSystemVariable == null || SelectedSystemVariable.IsEntry) return;
        // Will be called from UI
    }

    public void Delete(EnvVarItem item)
    {
        _envService.DeleteVariable(
            item.IsSystemVariable ? EnvironmentVariableTarget.Machine : EnvironmentVariableTarget.User,
            item.Name);

        // Remove from collection instead of reloading
        if (item.IsSystemVariable)
        {
            SystemVariables.Remove(item);
            SystemVariableCount = SystemVariables.Count;
        }
        else
        {
            UserVariables.Remove(item);
            UserVariableCount = UserVariables.Count;
        }

        StatusMessage = $"Deleted {(item.IsSystemVariable ? "system" : "user")} variable: {item.Name}";
    }

    public void Shrink(EnvVarItem item)
    {
        if (item.IsSystemVariable && !IsAdmin)
        {
            StatusMessage = "Administrator privileges required to modify system variables";
            return;
        }

        Dictionary<string, string> shrinkVars = EnvVarService.BuildShrinkVariableMap([.. UserVariables, .. SystemVariables]);
        string shrunkValue = string.Empty;
        if (item.IsComposite)
        {
            IEnumerable<string> entries = item.Children.Select(c => c.Name);
            IReadOnlyList<string> shrunkEntries = EnvVarService.ShrinkEntries(entries, shrinkVars);
            shrunkValue = string.Join(";", shrunkEntries);
        }
        else
        {
            shrunkValue = EnvVarService.ShrinkValue(item.Value, shrinkVars);
        }

        _envService.SetVariable(
            item.IsSystemVariable ? EnvironmentVariableTarget.Machine : EnvironmentVariableTarget.User,
            item.Name,
            shrunkValue);

        item.UpdateValue(shrunkValue);
        StatusMessage = $"Shrunk value in {item.Name}";
    }

    public void Expand(EnvVarItem item)
    {
        if (item.IsSystemVariable && !IsAdmin)
        {
            StatusMessage = "Administrator privileges required to modify system variables";
            return;
        }

        string expandedValue;
        if (item.IsComposite)
        {
            IEnumerable<string> entries = item.Children.Select(c => c.Name);
            IReadOnlyList<string> expandedEntries = EnvVarService.ExpandEntries(entries);
            expandedValue = string.Join(";", expandedEntries);
        }
        else
        {
            expandedValue = EnvVarService.ExpandValue(item.Value);
        }

        _envService.SetVariable(
            item.IsSystemVariable ? EnvironmentVariableTarget.Machine : EnvironmentVariableTarget.User,
            item.Name,
            expandedValue);

        item.UpdateValue(expandedValue);
        StatusMessage = item.IsComposite
            ? $"Expanded paths in {item.Name}"
            : $"Expanded value in {item.Name}";
    }

    public void Sort(EnvVarItem item)
    {
        if (item.IsSystemVariable && !IsAdmin)
        {
            StatusMessage = "Administrator privileges required to modify system variables";
            return;
        }

        if (!item.IsComposite)
        {
            return;
        }

        // Get all paths and sort them
        List<string> paths = item.Children.Select(c => c.Name).ToList();
        paths.Sort(StringComparer.OrdinalIgnoreCase);

        // Save the sorted value
        string newValue = string.Join(";", paths);

        _envService.SetVariable(
            item.IsSystemVariable ? EnvironmentVariableTarget.Machine : EnvironmentVariableTarget.User,
            item.Name,
            newValue);

        item.UpdateValue(newValue);
        StatusMessage = $"Sorted {item.Name}";
    }

    public void AddEntry(EnvVarItem item, string entry)
    {
        if (item.IsSystemVariable && !IsAdmin)
        {
            StatusMessage = "Administrator privileges required to modify system variables";
            return;
        }

        if (!item.IsComposite)
        {
            return;
        }

        // Add the new path to the existing paths
        List<string> existingPaths = item.Children.Select(c => c.Name).ToList();
        existingPaths.Add(entry);
        string newValue = string.Join(";", existingPaths);

        _envService.SetVariable(
            item.IsSystemVariable ? EnvironmentVariableTarget.Machine : EnvironmentVariableTarget.User,
            item.Name,
            newValue);

        item.UpdateValue(newValue);
        StatusMessage = $"Added path entry to {item.Name}";
    }

    public void DeleteEntry(EnvVarItem parent, EnvVarItem entry)
    {
        // Remove the child and reconstruct the parent PATH variable
        List<string> entries = parent.Children.Where(c => c != entry).Select(c => c.Name).ToList();
        string newValue = string.Join(";", entries);

        _envService.SetVariable(
            parent.IsSystemVariable ? EnvironmentVariableTarget.Machine : EnvironmentVariableTarget.User,
            parent.Name,
            newValue);

        parent.UpdateValue(newValue);
        StatusMessage = $"Removed entry [{entry.Value}] from [{parent.Name}]";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
