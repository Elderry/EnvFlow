using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using EnvFlow.Services;

namespace EnvFlow.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly EnvironmentVariableService _envService;
    private string _statusMessage = "Ready";
    private int _userVariableCount;
    private int _systemVariableCount;

    public ObservableCollection<EnvVariableItem> UserVariables { get; } = new();
    public ObservableCollection<EnvVariableItem> SystemVariables { get; } = new();

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

    public MainWindowViewModel()
    {
        _envService = new EnvironmentVariableService();
        LoadVariables();
    }

    private void LoadVariables()
    {
        StatusMessage = "Loading environment variables...";

        // Load user variables
        UserVariables.Clear();
        var userVars = _envService.GetUserVariables();
        foreach (var kvp in userVars.OrderBy(v => v.Key))
        {
            bool isPathLike = _envService.IsPathLike(kvp.Key);
            UserVariables.Add(new EnvVariableItem(kvp.Key, kvp.Value, isPathLike));
        }
        UserVariableCount = userVars.Count;

        // Load system variables
        SystemVariables.Clear();
        var systemVars = _envService.GetSystemVariables();
        foreach (var kvp in systemVars.OrderBy(v => v.Key))
        {
            bool isPathLike = _envService.IsPathLike(kvp.Key);
            SystemVariables.Add(new EnvVariableItem(kvp.Key, kvp.Value, isPathLike));
        }
        SystemVariableCount = systemVars.Count;

        StatusMessage = $"Loaded {UserVariableCount} user and {SystemVariableCount} system variables";
    }

    public void RefreshVariables()
    {
        LoadVariables();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
