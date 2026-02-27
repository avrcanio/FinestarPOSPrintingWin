namespace MozzartPrintReceiver.Configuration;

public sealed class ReceiverOptions
{
    public const string SectionName = "Receiver";

    public string Bind { get; set; } = "0.0.0.0";

    public int Port { get; set; } = 8089;

    public string? Token { get; set; }
}
