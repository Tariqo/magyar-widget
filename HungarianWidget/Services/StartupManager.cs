using Microsoft.Win32;

namespace HungarianWidget.Services;

public static class StartupManager
{
    private const string RunKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string ValueName = "MagyarWidget";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            var registeredPath = key?.GetValue(ValueName) as string;
            return !string.IsNullOrWhiteSpace(registeredPath)
                   && string.Equals(Unquote(registeredPath), Environment.ProcessPath, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled)
        {
            var processPath = Environment.ProcessPath
                              ?? throw new InvalidOperationException("Windows could not find the app executable path.");
            key.SetValue(ValueName, $"\"{processPath}\"");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    private static string Unquote(string value) => value.Trim().Trim('"');
}
