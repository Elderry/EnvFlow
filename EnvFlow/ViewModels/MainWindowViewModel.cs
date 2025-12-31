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

        // Save expanded state before clearing
        var expandedUserVars = new HashSet<string>(
            UserVariables.Where(v => v.IsExpanded).Select(v => v.Name),
            StringComparer.OrdinalIgnoreCase);
        var expandedSystemVars = new HashSet<string>(
            SystemVariables.Where(v => v.IsExpanded).Select(v => v.Name),
            StringComparer.OrdinalIgnoreCase);

        // Load user variables
        UserVariables.Clear();
        var userVars = _envService.GetUserVariables(out var volatileVariables);
        foreach (var kvp in userVars.OrderBy(v => v.Key))
        {
            bool isPathLike = _envService.IsPathLike(kvp.Key);
            bool isVolatile = volatileVariables.Contains(kvp.Key);
            var item = new EnvVariableItem(kvp.Key, kvp.Value, isPathLike, isVolatile);
            
            // Restore expanded state
            if (expandedUserVars.Contains(kvp.Key))
            {
                item.IsExpanded = true;
            }
            
            UserVariables.Add(item);
        }
        UserVariableCount = userVars.Count;

        // Load system variables
        SystemVariables.Clear();
        var systemVars = _envService.GetSystemVariables(out var systemVolatileVariables);
        foreach (var kvp in systemVars.OrderBy(v => v.Key))
        {
            bool isPathLike = _envService.IsPathLike(kvp.Key);
            bool isVolatile = systemVolatileVariables.Contains(kvp.Key);
            var item = new EnvVariableItem(kvp.Key, kvp.Value, isPathLike, isVolatile);
            
            // Restore expanded state
            if (expandedSystemVars.Contains(kvp.Key))
            {
                item.IsExpanded = true;
            }
            
            SystemVariables.Add(item);
        }
        SystemVariableCount = systemVars.Count;

        StatusMessage = $"Loaded {UserVariableCount} user and {SystemVariableCount} system variables";
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
