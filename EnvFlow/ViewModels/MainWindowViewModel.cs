using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using EnvFlow.Services;
using EnvFlow.Helpers;

namespace EnvFlow.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly EnvironmentVariableService _envService;
    private string _statusMessage = "Ready";
    private int _userVariableCount;
    private int _systemVariableCount;
    private bool _isAdmin;
    private EnvVariableItem? _selectedUserVariable;
    private EnvVariableItem? _selectedSystemVariable;

    public ObservableCollection<EnvVariableItem> UserVariables { get; } = new();
    public ObservableCollection<EnvVariableItem> SystemVariables { get; } = new();

    public EnvVariableItem? SelectedUserVariable
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

    public EnvVariableItem? SelectedSystemVariable
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

    public MainWindowViewModel()
    {
        _envService = new EnvironmentVariableService();
        IsAdmin = AdminHelper.IsRunningAsAdmin();
        LoadVariables();
    }

    private void LoadVariables()
    {
        StatusMessage = "Loading environment variables...";

        // Load user variables - update in place to avoid blink
        var userVars = _envService.GetUserVariables(out var volatileVariables);
        UpdateVariableCollection(UserVariables, userVars, volatileVariables);
        UserVariableCount = userVars.Count;

        // Load system variables - update in place to avoid blink
        var systemVars = _envService.GetSystemVariables(out var systemVolatileVariables);
        UpdateVariableCollection(SystemVariables, systemVars, systemVolatileVariables);
        SystemVariableCount = systemVars.Count;

        StatusMessage = $"Loaded {UserVariableCount} user and {SystemVariableCount} system variables";
    }

    private void UpdateVariableCollection(ObservableCollection<EnvVariableItem> collection, 
        Dictionary<string, string> newVars, HashSet<string> volatileVars)
    {
        // Create a dictionary of existing items for quick lookup
        var existingItems = collection.ToDictionary(v => v.Name, StringComparer.OrdinalIgnoreCase);
        var processedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Update existing items or add new ones (in sorted order)
        var sortedVars = newVars.OrderBy(v => v.Key).ToList();
        int currentIndex = 0;

        foreach (var kvp in sortedVars)
        {
            processedNames.Add(kvp.Key);

            if (existingItems.TryGetValue(kvp.Key, out var existingItem))
            {
                // Update existing item
                bool needsUpdate = existingItem.Value != kvp.Value;
                
                if (needsUpdate)
                {
                    // Save expanded state
                    bool wasExpanded = existingItem.IsExpanded;
                    
                    // Remove and recreate to update children properly
                    collection.Remove(existingItem);
                    
                    bool isVolatile = volatileVars.Contains(kvp.Key);
                    var newItem = new EnvVariableItem(kvp.Key, kvp.Value, isVolatile)
                    {
                        IsExpanded = wasExpanded
                    };
                    
                    // Insert at correct position
                    if (currentIndex < collection.Count)
                        collection.Insert(currentIndex, newItem);
                    else
                        collection.Add(newItem);
                }
                else
                {
                    // Move to correct position if needed
                    int existingIndex = collection.IndexOf(existingItem);
                    if (existingIndex != currentIndex)
                    {
                        collection.Move(existingIndex, currentIndex);
                    }
                }
            }
            else
            {
                // Add new item
                bool isVolatile = volatileVars.Contains(kvp.Key);
                var newItem = new EnvVariableItem(kvp.Key, kvp.Value, isVolatile);
                
                if (currentIndex < collection.Count)
                    collection.Insert(currentIndex, newItem);
                else
                    collection.Add(newItem);
            }

            currentIndex++;
        }

        // Remove items that no longer exist
        for (int i = collection.Count - 1; i >= 0; i--)
        {
            if (!processedNames.Contains(collection[i].Name))
            {
                collection.RemoveAt(i);
            }
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
        if (SelectedUserVariable == null || SelectedUserVariable.IsChild) return;
        // Will be called from UI
    }

    public void EditSystemVariable()
    {
        if (!IsAdmin || SelectedSystemVariable == null || SelectedSystemVariable.IsChild) return;
        // Will be called from UI
    }

    public void DeleteUserVariable()
    {
        if (SelectedUserVariable == null || SelectedUserVariable.IsChild) return;
        
        try
        {
            _envService.DeleteUserVariable(SelectedUserVariable.Name);
            StatusMessage = $"Deleted user variable: {SelectedUserVariable.Name}";
            LoadVariables();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting variable: {ex.Message}";
        }
    }

    public void DeleteSystemVariable()
    {
        if (!IsAdmin || SelectedSystemVariable == null || SelectedSystemVariable.IsChild) return;
        
        try
        {
            _envService.DeleteSystemVariable(SelectedSystemVariable.Name);
            StatusMessage = $"Deleted system variable: {SelectedSystemVariable.Name}";
            LoadVariables();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting variable: {ex.Message}";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
