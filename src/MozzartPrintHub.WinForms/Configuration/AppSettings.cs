namespace MozzartPrintHub.WinForms.Configuration;

public sealed class AppSettings
{
    public ReceiverSettings Receiver { get; set; } = new();
    public EmulatorSettings Emulator { get; set; } = new();
    public PrintSettings Print { get; set; } = new();
}

public sealed class ReceiverSettings
{
    public string Bind { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 8089;
    public string Token { get; set; } = string.Empty;
}

public sealed class EmulatorSettings
{
    public bool Enabled { get; set; } = true;
    public string Bind { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 9100;
    public int MaxHistoryEntries { get; set; } = 200;
    public int MaxJobBytes { get; set; } = 262144;
    public int IdleReadTimeoutSeconds { get; set; } = 5;
}

public sealed class PrintSettings
{
    public string DefaultPrinterName { get; set; } = string.Empty;
    public bool AutoPrint { get; set; } = true;
    public bool StartWithWindows { get; set; }
}
