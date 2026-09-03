using System;
using System.Reflection;
using Microsoft.Win32;

namespace DittoBuddy;

internal static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DittoBuddy";

    private static string ExePath => Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(ValueName) as string == ExePath;
        }
    }

    public static void Enable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true);
        key.SetValue(ValueName, ExePath);
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
        key?.DeleteValue(ValueName, false);
    }
}
