using System.Text.Json.Serialization;

namespace MozzartPrintReceiver.Contracts;

public sealed class ApiError
{
    public ApiError(string code, string message, IReadOnlyCollection<string>? details = null)
    {
        Code = code;
        Message = message;
        Details = details;
    }

    [JsonPropertyName("code")]
    public string Code { get; }

    [JsonPropertyName("message")]
    public string Message { get; }

    [JsonPropertyName("details")]
    public IReadOnlyCollection<string>? Details { get; }
}
