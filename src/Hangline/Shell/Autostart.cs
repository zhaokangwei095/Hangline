using Microsoft.Win32;

namespace Hangline.Shell;

/// <summary>
/// Start with Windows. The original uses SMAppService; the equivalent a portable
/// app can do without signing is an HKCU Run entry pointing at the current exe,
/// which is why moving the folder means toggling this off and on again.
/// </summary>
public static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Hangline";

    public static bool IsEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string path
                   && string.Equals(path.Trim('"'), Environment.ProcessPath, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Log.Error("autostart read", ex);
            return false;
        }
    }

    public static void Set(bool on)
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (on) key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
            else key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch (Exception ex)
        {
            Log.Error("autostart write", ex);
        }
    }
}
