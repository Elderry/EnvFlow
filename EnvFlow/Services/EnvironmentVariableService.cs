using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace EnvFlow.Services;

public class EnvironmentVariableService
{
    public Dictionary<string, string> GetUserVariables()
    {
        return GetUserVariables(out _);
    }

    public Dictionary<string, string> GetUserVariables(out HashSet<string> volatileVariables)
    {
        var variables = new Dictionary<string, string>();
        volatileVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        try
        {
            // First, read from registry to get unexpanded values
            using var key = Registry.CurrentUser.OpenSubKey(@"Environment");
            if (key != null)
            {
                foreach (var valueName in key.GetValueNames())
                {
                    var value = key.GetValue(valueName, "", RegistryValueOptions.DoNotExpandEnvironmentNames);
                    if (value != null)
                    {
                        variables[valueName] = value.ToString()!;
                    }
                }
            }
            
            // Also read volatile environment variables (like APPDATA, TEMP, USERPROFILE)
            using var volatileKey = Registry.CurrentUser.OpenSubKey(@"Volatile Environment");
            if (volatileKey != null)
            {
                foreach (var valueName in volatileKey.GetValueNames())
                {
                    // Only add if not already in registry variables
                    if (!variables.ContainsKey(valueName))
                    {
                        var value = volatileKey.GetValue(valueName, "", RegistryValueOptions.DoNotExpandEnvironmentNames);
                        if (value != null)
                        {
                            variables[valueName] = value.ToString()!;
                            volatileVariables.Add(valueName); // Mark as volatile
                        }
                    }
                }
            }
        }
        catch (Exception)
        {
            // Fallback to Environment API if registry access fails
            foreach (var k in Environment.GetEnvironmentVariables(EnvironmentVariableTarget.User).Keys)
            {
                var value = Environment.GetEnvironmentVariable(k.ToString()!, EnvironmentVariableTarget.User);
                if (value != null)
                {
                    variables[k.ToString()!] = value;
                }
            }
        }
        
        return variables;
    }

    public Dictionary<string, string> GetSystemVariables()
    {
        return GetSystemVariables(out _);
    }

    public Dictionary<string, string> GetSystemVariables(out HashSet<string> volatileVariables)
    {
        var variables = new Dictionary<string, string>();
        volatileVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        try
        {
            // Read from registry to get unexpanded values (editable variables)
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment");
            if (key != null)
            {
                foreach (var valueName in key.GetValueNames())
                {
                    var value = key.GetValue(valueName, "", RegistryValueOptions.DoNotExpandEnvironmentNames);
                    if (value != null)
                    {
                        variables[valueName] = value.ToString()!;
                    }
                }
            }
            
            // Also get dynamic system variables from the current process environment
            // These include ProgramFiles, SystemRoot, etc. that Windows computes at runtime
            var processVars = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Process);
            var machineVars = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Machine);
            
            foreach (var key2 in processVars.Keys)
            {
                var keyName = key2.ToString()!;
                
                // If it's in process but not in Machine registry, it's a dynamic system variable
                if (!machineVars.Contains(keyName) && !variables.ContainsKey(keyName))
                {
                    var value = processVars[keyName]?.ToString();
                    if (value != null)
                    {
                        // Filter to only system-level dynamic variables (not user or process specific)
                        if (IsDynamicSystemVariable(keyName))
                        {
                            variables[keyName] = value;
                            volatileVariables.Add(keyName); // Mark as volatile/dynamic
                        }
                    }
                }
            }
        }
        catch (Exception)
        {
            // May require admin privileges
        }
        
        return variables;
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
