using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
