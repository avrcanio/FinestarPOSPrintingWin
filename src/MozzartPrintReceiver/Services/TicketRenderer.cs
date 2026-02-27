using System.Globalization;
using System.Text;
using MozzartPrintReceiver.Contracts;

namespace MozzartPrintReceiver.Services;

public sealed class TicketRenderer
{
    private const int Width = 42;

    public string Render(BarTicketPayload payload)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Center("BAR TICKET"));
        sb.AppendLine(new string('-', Width));
        sb.AppendLine($"Time : {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}");

        if (!string.IsNullOrWhiteSpace(payload.Table))
        {
            sb.AppendLine($"Table: {payload.Table}");
        }

        if (!string.IsNullOrWhiteSpace(payload.Waiter))
        {
            sb.AppendLine($"Waiter: {payload.Waiter}");
        }

        if (payload.RoundNumber.HasValue)
        {
            sb.AppendLine($"Round: {payload.RoundNumber.Value}");
        }

        sb.AppendLine(new string('-', Width));

        foreach (var item in payload.Items)
        {
            var qty = item.Qty.ToString("0.##", CultureInfo.InvariantCulture);
            sb.AppendLine($"{qty} x {item.Name}");
            if (!string.IsNullOrWhiteSpace(item.Note))
            {
                sb.AppendLine($"  note: {item.Note}");
            }
        }

        sb.AppendLine(new string('-', Width));
        sb.AppendLine();
        sb.AppendLine();

        return sb.ToString();
    }

    private static string Center(string text)
    {
        if (text.Length >= Width)
        {
            return text;
        }

        var left = (Width - text.Length) / 2;
        return new string(' ', left) + text;
    }
}
