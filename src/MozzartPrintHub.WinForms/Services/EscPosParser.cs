using System.Text;
using MozzartPrintHub.WinForms.Domain;

namespace MozzartPrintHub.WinForms.Services;

public sealed class EscPosParser
{
    public (ParsedEscPosDocument Document, int UnknownCommandCount) Parse(ReadOnlySpan<byte> data)
    {
        var document = new ParsedEscPosDocument();
        var align = "left";
        var bold = false;
        var unknown = 0;
        var current = new StringBuilder();

        var i = 0;
        while (i < data.Length)
        {
            var b = data[i];

            if (b == 0x0A)
            {
                FlushLine(document, current, align, bold);
                i++;
                continue;
            }

            if (b == 0x1B && i + 2 < data.Length)
            {
                var cmd = data[i + 1];
                var n = data[i + 2];
                if (cmd == 0x61)
                {
                    align = n switch
                    {
                        1 => "center",
                        2 => "right",
                        _ => "left"
                    };
                    i += 3;
                    continue;
                }

                if (cmd == 0x45)
                {
                    bold = n != 0;
                    i += 3;
                    continue;
                }
            }

            if (b == 0x1D && i + 2 < data.Length && data[i + 1] == 0x56)
            {
                FlushLine(document, current, align, bold);
                document.Lines.Add(new ParsedLine
                {
                    Text = "[CUT]",
                    Align = "center",
                    Bold = false,
                    IsCutMarker = true
                });

                i += 3;
                continue;
            }

            if (b < 0x20 || b > 0x7E)
            {
                current.Append('?');
                unknown++;
            }
            else
            {
                current.Append((char)b);
            }

            i++;
        }

        FlushLine(document, current, align, bold);
        return (document, unknown);
    }

    private static void FlushLine(ParsedEscPosDocument document, StringBuilder current, string align, bool bold)
    {
        if (current.Length == 0)
        {
            return;
        }

        document.Lines.Add(new ParsedLine
        {
            Text = current.ToString(),
            Align = align,
            Bold = bold
        });
        current.Clear();
    }
}
