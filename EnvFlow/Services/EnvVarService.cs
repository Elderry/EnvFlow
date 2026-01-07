using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using EnvFlow.Models;

using Microsoft.Win32;

namespace EnvFlow.Services;

public class EnvVarService
{
    private List<EnvVarItem>? _cachedUserVariables;

    public List<EnvVarItem> GetUserVariables()
    {
        if (_cachedUserVariables != null)
            return _cachedUserVariables;

        var items = ReadRegistryVariables(Registry.CurrentUser, @"Environment", isSystemVariable: false, isReadOnly: false)
            .Concat(ReadRegistryVariables(Registry.CurrentUser, @"Volatile Environment", isSystemVariable: false, isReadOnly: true))
            .ToList();

        _cachedUserVariables = items;
        return items;
    }

    public List<EnvVarItem> GetSystemVariables()
    {
        // Read from registry to get unexpanded values (editable variables)
        var items = ReadRegistryVariables(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", isSystemVariable: true, isReadOnly: false);
        
        // Also get dynamic system variables from the current process environment
        // These include ProgramFiles, SystemRoot, etc. that Windows computes at runtime
        var dynamicVars = GetDynamicSystemVariables(items);
        
        return items.Concat(dynamicVars).ToList();
    }

    private IEnumerable<EnvVarItem> GetDynamicSystemVariables(IEnumerable<EnvVarItem> existingItems)
    {
        var processVars = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Process);
        var machineVars = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Machine);
        var existingNames = new HashSet<string>(existingItems.Select(i => i.Name), StringComparer.OrdinalIgnoreCase);
        
        foreach (var key in processVars.Keys)
        {
            var keyName = key.ToString()!;
            
            // If it's in process but not in Machine registry, it's a dynamic system variable
            if (!machineVars.Contains(keyName) && !existingNames.Contains(keyName))
            {
                var value = processVars[keyName]?.ToString();
                if (value != null && IsDynamicSystemVariable(keyName))
                {
                    yield return new EnvVarItem(keyName, value, isReadOnly: true, isSystemVariable: true);
                }
            }
        }
    }

    private IEnumerable<EnvVarItem> ReadRegistryVariables(RegistryKey rootKey, string subKeyPath, bool isSystemVariable, bool isReadOnly)
    {
        using RegistryKey? key = rootKey.OpenSubKey(subKeyPath);
        if (key != null)
        {
            foreach (string valueName in key.GetValueNames())
            {
                object value = key.GetValue(valueName, "", RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (value != null)
                {
                    var item = new EnvVarItem(valueName, value.ToString()!, isReadOnly);
                    item.IsSystemVariable = isSystemVariable;
                    yield return item;
                }
            }
        }
    }

    private bool IsDynamicSystemVariable(string varName)
    {
        // Known dynamic system variables that Windows provides
        var dynamicVars = new[]
        {
            "ProgramFiles", "ProgramFiles(x86)", "ProgramW6432",
            "CommonProgramFiles", "CommonProgramFiles(x86)", "CommonProgramW6432",
            "SystemRoot", "SystemDrive", "windir", "ProgramData"
        };
        
        return dynamicVars.Contains(varName, StringComparer.OrdinalIgnoreCase);
    }

    public List<string> ParsePathVariable(string pathValue)
    {
        if (string.IsNullOrEmpty(pathValue))
            return new List<string>();

        return pathValue
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();
    }

    public bool IsPathLike(string variableName)
    {
        var pathVariables = new[] { "PATH", "PSMODULEPATH", "CLASSPATH" };
        return pathVariables.Contains(variableName.ToUpper());
    }

    public bool PathExists(string path)
    {
        try
        {
            // Expand environment variables before checking
            var expandedPath = Environment.ExpandEnvironmentVariables(path);
            return Directory.Exists(expandedPath) || File.Exists(expandedPath);
        }
        catch
        {
            return false;
        }
    }

    public void SetUserVariable(string name, string value)
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Environment", true);
        if (key != null)
        {
            var valueKind = IsExpandableString(value) ? RegistryValueKind.ExpandString : RegistryValueKind.String;
            key.SetValue(name, value, valueKind);
        }
        
        // Notify system of environment change
        NotifyEnvironmentChange();
    }

    public void SetSystemVariable(string name, string value)
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", true);
        if (key != null)
        {
            var valueKind = IsExpandableString(value) ? RegistryValueKind.ExpandString : RegistryValueKind.String;
            key.SetValue(name, value, valueKind);
        }
        
        // Notify system of environment change
        NotifyEnvironmentChange();
    }

    public void DeleteUserVariable(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Environment", true);
        if (key != null)
        {
            key.DeleteValue(name, false);
        }
        
        // Notify system of environment change
        NotifyEnvironmentChange();
    }

    public void DeleteSystemVariable(string name)
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", true);
        if (key != null)
        {
            key.DeleteValue(name, false);
        }
        
        // Notify system of environment change
        NotifyEnvironmentChange();
    }

    private bool IsExpandableString(string value)
    {
        // Check if the value contains environment variable references like %USERPROFILE%, %SystemRoot%, etc.
        return value.Contains('%');
    }

    private void NotifyEnvironmentChange()
    {
        // This notifies Windows that environment variables have changed
        // so other processes can pick up the changes
        try
        {
            const int HWND_BROADCAST = 0xffff;
            const int WM_SETTINGCHANGE = 0x001A;
            
            // Using P/Invoke to notify system
            SendMessageTimeout(
                (IntPtr)HWND_BROADCAST,
                WM_SETTINGCHANGE,
                IntPtr.Zero,
                "Environment",
                SendMessageTimeoutFlags.SMTO_ABORTIFHUNG,
                5000,
                out _);
        }
        catch
        {
            // Ignore errors in notification
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        int Msg,
        IntPtr wParam,
        string lParam,
        SendMessageTimeoutFlags fuFlags,
        uint uTimeout,
        out IntPtr lpdwResult);

    [Flags]
    private enum SendMessageTimeoutFlags : uint
    {
        SMTO_NORMAL = 0x0,
        SMTO_BLOCK = 0x1,
        SMTO_ABORTIFHUNG = 0x2,
        SMTO_NOTIMEOUTIFNOTHUNG = 0x8
    }
}
