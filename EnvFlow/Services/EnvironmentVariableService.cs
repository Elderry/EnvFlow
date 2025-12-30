using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EnvFlow.Services;

public class EnvironmentVariableService
{
    public Dictionary<string, string> GetUserVariables()
    {
        var variables = new Dictionary<string, string>();
        
        foreach (var key in Environment.GetEnvironmentVariables(EnvironmentVariableTarget.User).Keys)
        {
            var value = Environment.GetEnvironmentVariable(key.ToString()!, EnvironmentVariableTarget.User);
            if (value != null)
            {
                variables[key.ToString()!] = value;
            }
        }
        
        return variables;
    }

    public Dictionary<string, string> GetSystemVariables()
    {
        var variables = new Dictionary<string, string>();
        
        try
        {
            foreach (var key in Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Machine).Keys)
            {
                var value = Environment.GetEnvironmentVariable(key.ToString()!, EnvironmentVariableTarget.Machine);
                if (value != null)
                {
                    variables[key.ToString()!] = value;
                }
            }
        }
        catch (Exception)
        {
            // May require admin privileges
        }
        
        return variables;
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
        var pathVariables = new[] { "PATH", "PATHEXT", "PSMODULEPATH", "CLASSPATH" };
        return pathVariables.Contains(variableName.ToUpper());
    }

    public bool PathExists(string path)
    {
        try
        {
            return Directory.Exists(path) || File.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    public void SetUserVariable(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.User);
    }

    public void SetSystemVariable(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.Machine);
    }

    public void DeleteUserVariable(string name)
    {
        Environment.SetEnvironmentVariable(name, null, EnvironmentVariableTarget.User);
    }

    public void DeleteSystemVariable(string name)
    {
        Environment.SetEnvironmentVariable(name, null, EnvironmentVariableTarget.Machine);
    }
}
