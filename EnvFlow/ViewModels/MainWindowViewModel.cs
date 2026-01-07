using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using EnvFlow.Services;
using EnvFlow.Helpers;
using EnvFlow.Models;

namespace EnvFlow.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly EnvVarService _envService;
    private string _statusMessage = "Ready";
    private int _userVariableCount;
    private int _systemVariableCount;
    private bool _isAdmin;
    private EnvVarItem? _selectedUserVariable;
    private EnvVarItem? _selectedSystemVariable;

    public ObservableCollection<EnvVarItem> UserVariables { get; } = new();
    public ObservableCollection<EnvVarItem> SystemVariables { get; } = new();

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

    public void DeleteUserVariable()
    {
        if (SelectedUserVariable == null || SelectedUserVariable.IsEntry) return;
        
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
        if (!IsAdmin || SelectedSystemVariable == null || SelectedSystemVariable.IsEntry) return;
        
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
