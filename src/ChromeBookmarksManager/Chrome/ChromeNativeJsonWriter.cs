using System.Globalization;
using System.Text.Json;

namespace ChromeBookmarksManager.Chrome;

internal static class ChromeNativeJsonWriter
{
    private const int IndentSize = 3;
    private const string NewLine = "\r\n";

    public static void WriteIndent(TextWriter writer, int depth)
    {
        if (depth <= 0)
        {
            return;
        }

        writer.Write(new string(' ', checked(depth * IndentSize)));
    }

    public static void WritePropertyPrefix(
        TextWriter writer,
        string propertyName,
        int depth,
        ref bool first)
    {
        if (!first)
        {
            writer.Write(',');
            writer.Write(NewLine);
        }
        else
        {
            writer.Write(NewLine);
            first = false;
        }

        WriteIndent(writer, depth);
        WriteString(writer, propertyName);
        writer.Write(": ");
    }

    public static void CloseObject(
        TextWriter writer,
        int depth,
        bool hasProperties)
    {
        if (hasProperties)
        {
            writer.Write(NewLine);
            WriteIndent(writer, depth);
        }

        writer.Write('}');
    }

    public static void WriteString(
        TextWriter writer,
        string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        writer.Write('"');

        foreach (var character in value)
        {
            switch (character)
            {
                case '"':
                    writer.Write("\\\"");
                    break;

                case '\\':
                    writer.Write("\\\\");
                    break;

                case '\b':
                    writer.Write("\\b");
                    break;

                case '\f':
                    writer.Write("\\f");
                    break;

                case '\n':
                    writer.Write("\\n");
                    break;

                case '\r':
                    writer.Write("\\r");
                    break;

                case '\t':
                    writer.Write("\\t");
                    break;

                // Chromium's native Bookmarks JSON writer preserves Unicode
                // text but escapes '<' for HTML/script safety.
                case '<':
                    writer.Write("\\u003C");
                    break;

                default:
                    if (character < 0x20)
                    {
                        writer.Write("\\u");
                        writer.Write(
                            ((int)character).ToString(
                                "X4",
                                CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        writer.Write(character);
                    }

                    break;
            }
        }

        writer.Write('"');
    }

    public static void WriteJsonElement(
        TextWriter writer,
        JsonElement element,
        int depth,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                WriteJsonObject(
                    writer,
                    element,
                    depth,
                    cancellationToken);
                break;

            case JsonValueKind.Array:
                WriteJsonArray(
                    writer,
                    element,
                    depth,
                    cancellationToken);
                break;

            case JsonValueKind.String:
                WriteString(
                    writer,
                    element.GetString() ?? string.Empty);
                break;

            case JsonValueKind.Number:
                writer.Write(element.GetRawText());
                break;

            case JsonValueKind.True:
                writer.Write("true");
                break;

            case JsonValueKind.False:
                writer.Write("false");
                break;

            case JsonValueKind.Null:
                writer.Write("null");
                break;

            default:
                throw new InvalidOperationException(
                    "Undefined JSON values cannot be written to a Chrome Bookmarks file.");
        }
    }

    private static void WriteJsonObject(
        TextWriter writer,
        JsonElement element,
        int depth,
        CancellationToken cancellationToken)
    {
        writer.Write('{');
        var first = true;

        foreach (var property in element.EnumerateObject())
        {
            cancellationToken.ThrowIfCancellationRequested();

            WritePropertyPrefix(
                writer,
                property.Name,
                depth + 1,
                ref first);

            WriteJsonElement(
                writer,
                property.Value,
                depth + 1,
                cancellationToken);
        }

        CloseObject(
            writer,
            depth,
            hasProperties: !first);
    }

    private static void WriteJsonArray(
        TextWriter writer,
        JsonElement element,
        int depth,
        CancellationToken cancellationToken)
    {
        writer.Write("[ ");

        var first = true;
        foreach (var item in element.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!first)
            {
                writer.Write(", ");
            }

            first = false;

            WriteJsonElement(
                writer,
                item,
                depth,
                cancellationToken);
        }

        writer.Write(" ]");
    }
}
