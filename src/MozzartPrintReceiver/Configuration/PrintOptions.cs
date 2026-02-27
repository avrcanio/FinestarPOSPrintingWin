namespace MozzartPrintReceiver.Configuration;

public sealed class PrintOptions
{
    public const string SectionName = "Print";

    public string TempDir { get; set; } = @"C:\ProgramData\MozzartPrintReceiver\spool";

    public string SumatraPath { get; set; } = @"C:\Tools\SumatraPDF\SumatraPDF.exe";

    public int SumatraTimeoutSeconds { get; set; } = 60;
}
