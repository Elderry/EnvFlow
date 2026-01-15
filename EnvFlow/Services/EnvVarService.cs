using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

using EnvFlow.Models;

using Microsoft.Win32;

namespace EnvFlow.Services;

public class EnvVarService
{
    /// <summary>
    /// Dynamic / computed Windows environment variables we surface as read-only system vars.
    /// Source: Microsoft Learn (USMT) "Recognized environment variables"
    /// https://learn.microsoft.com/en-us/windows/deployment/usmt/usmt-recognized-environment-variables
    /// </summary>
    private static readonly string[] DynamicSystemVariableNames =
    [
        "ALLUSERSPROFILE",
        "CommonProgramFiles",
        "CommonProgramFiles(X86)",
        "CommonProgramW6432",
        "COMPUTERNAME",
        "ProgramData",
        "ProgramFiles",
        "ProgramFiles(X86)",
        "ProgramW6432",
        "PUBLIC",
        "SystemDrive",
        "SystemRoot"
    ];

    public List<EnvVarItem> GetUserVariables() =>
        [
            ..ReadRegistryVariables(Registry.CurrentUser, @"Environment", isSystemVariable: false, isReadOnly: false),
            ..ReadRegistryVariables(Registry.CurrentUser, @"Volatile Environment", isSystemVariable: false, isReadOnly: true)
        ];

    public List<EnvVarItem> GetSystemVariables() =>
        [
            ..ReadRegistryVariables(
                Registry.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment",
                isSystemVariable: true,
                isReadOnly: false),
            ..GetDynamicSystemVariables()
        ];

    public static Dictionary<string, string> BuildShrinkVariableMap(IEnumerable<EnvVarItem> variables)
    {
        Dictionary<string, string> map = [];

        foreach (EnvVarItem item in variables.Where(variable => variable.IsReadOnly))
        {
            string value = Environment.GetEnvironmentVariable(item.Name, EnvironmentVariableTarget.Process)!;
            map[item.Name] = value;
        }

        return map;
    }

    public static List<string> ShrinkEntries(IEnumerable<string> entries, Dictionary<string, string> variables)
    {
        List<string> result = [];
        foreach (string entry in entries)
        {
            result.Add(ShrinkValue(entry, variables));
        }

        return result;
    }

    public static List<string> ExpandEntries(IEnumerable<string> entries)
    {
        List<string> result = [];
        foreach (string entry in entries)
        {
            result.Add(ExpandValue(entry));
        }

        return result;
    }

    public static string ExpandValue(string value)
    {
        return string.IsNullOrEmpty(value) ? value : Environment.ExpandEnvironmentVariables(value);
    }

    public static string ShrinkValue(string value, Dictionary<string, string> variableSource)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        string expandedValue = Environment.ExpandEnvironmentVariables(value);

        string? bestMatch = null;
        int longestMatchLength = 0;

        foreach (KeyValuePair<string, string> pair in variableSource)
        {
            string sourceValue = pair.Value.TrimEnd('\\');
            if (sourceValue.Length == 0)
            {
                continue;
            }

            if (expandedValue.StartsWith(sourceValue, StringComparison.OrdinalIgnoreCase) && sourceValue.Length > longestMatchLength)
            {
                bestMatch = pair.Key;
                longestMatchLength = sourceValue.Length;
            }
        }

        if (bestMatch == null)
        {
            return value;
        }

        string matchedValue = variableSource[bestMatch].TrimEnd('\\');
        string remainder = expandedValue[matchedValue.Length..].TrimStart('\\');
        return string.IsNullOrEmpty(remainder)
            ? $"%{bestMatch}%"
            : $"%{bestMatch}%\\{remainder}";
    }

    private static List<EnvVarItem> GetDynamicSystemVariables()
    {
        List<EnvVarItem> items = [];
        foreach (string name in DynamicSystemVariableNames)
        {
            string? value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
            if (value != null)
            {
                items.Add(new EnvVarItem(name, value, isReadOnly: true, isSystemVariable: true));
            }
        }
        return items;
    }

    private static List<EnvVarItem> ReadRegistryVariables(RegistryKey rootKey, string subKeyPath, bool isSystemVariable, bool isReadOnly)
    {
        List<EnvVarItem> items = [];
        using RegistryKey? key = rootKey.OpenSubKey(subKeyPath);

        if (key == null)
        {
            return items;
        }

        foreach (string valueName in key.GetValueNames())
        {
            object? value = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (value != null)
            {
                items.Add(new EnvVarItem(valueName, value.ToString()!, isReadOnly, isSystemVariable));
            }
        }
        return items;
    }

    public void SetVariable(EnvironmentVariableTarget target, string name, string value)
    {
        using RegistryKey key = target switch
        {
            EnvironmentVariableTarget.User => Registry.CurrentUser.OpenSubKey(@"Environment", writable: true)!,
            EnvironmentVariableTarget.Machine => Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", writable: true)!,
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Only User and Machine targets are supported")
        };

        RegistryValueKind valueKind = IsExpandable(value) ? RegistryValueKind.ExpandString : RegistryValueKind.String;
        key.SetValue(name, value, valueKind);

        // Notify system of environment change
        NotifyEnvironmentChange();
    }

    public void DeleteVariable(EnvironmentVariableTarget target, string name)
    {
        using RegistryKey key = target switch
        {
            EnvironmentVariableTarget.User => Registry.CurrentUser.OpenSubKey(@"Environment", writable: true)!,
            EnvironmentVariableTarget.Machine => Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", writable: true)!,
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Only User and Machine targets are supported")
        };

        key.DeleteValue(name, false);

        // Notify system of environment change
        NotifyEnvironmentChange();
    }

    private static bool IsExpandable(string value)
    {
        // Check if the value contains environment variable references like %USERPROFILE%, %SystemRoot%, etc.
        return value.Contains('%');
    }

    private static void NotifyEnvironmentChange()
    {
        // This notifies Windows that environment variables have changed
        // so other processes can pick up the changes
        const int HWND_BROADCAST = 0xffff;
        const int WM_SETTINGCHANGE = 0x001A;

        // Using P/Invoke to notify system
        SendMessageTimeout(
            HWND_BROADCAST,
            WM_SETTINGCHANGE,
            IntPtr.Zero,
            "Environment",
            SendMessageTimeoutFlags.SMTO_ABORTIFHUNG,
            5000,
            out _);
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
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
