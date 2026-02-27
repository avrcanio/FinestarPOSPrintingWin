using Microsoft.Win32;

namespace MozzartPrintHub.WinForms.Services;

public sealed class StartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly string _appName;

    public StartupRegistrationService(string appName)
    {
        _appName = appName;
    }

    public void Apply(bool enabled, string executablePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (key is null)
        {
            throw new InvalidOperationException("Unable to open Run registry key.");
        }

        if (enabled)
        {
            key.SetValue(_appName, $"\"{executablePath}\"");
            return;
        }

        if (key.GetValue(_appName) is not null)
        {
            key.DeleteValue(_appName, throwOnMissingValue: false);
        }
    }
}
