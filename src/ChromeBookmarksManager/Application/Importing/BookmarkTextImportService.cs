using System.Net;

namespace ChromeBookmarksManager.Application.Importing;

public sealed record BookmarkTextImportItem(
    string Name,
    string Url,
    int LineNumber);

public sealed record BookmarkTextImportError(
    int LineNumber,
    string Value);

public sealed record BookmarkTextImportResult(
    IReadOnlyList<BookmarkTextImportItem> Items,
    IReadOnlyList<BookmarkTextImportError> Errors);

public static class BookmarkTextImportService
{
    public static BookmarkTextImportResult Parse(string? text)
    {
        var items = new List<BookmarkTextImportItem>();
        var errors = new List<BookmarkTextImportError>();

        if (string.IsNullOrWhiteSpace(text))
        {
            return new BookmarkTextImportResult(items, errors);
        }

        using var reader = new StringReader(text);
        var lineNumber = 0;
        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            var value = line.Trim();
            if (value.Length == 0)
            {
                continue;
            }

            if (!TryNormalizeUrl(value, out var normalizedUrl, out var uri))
            {
                errors.Add(new BookmarkTextImportError(lineNumber, value));
                continue;
            }

            items.Add(
                new BookmarkTextImportItem(
                    DeriveName(uri, normalizedUrl),
                    normalizedUrl,
                    lineNumber));
        }

        return new BookmarkTextImportResult(items, errors);
    }

    private static bool TryNormalizeUrl(
        string value,
        out string normalizedUrl,
        out Uri uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute) &&
            IsExplicitAbsoluteUrl(value, absolute))
        {
            normalizedUrl = value;
            uri = absolute;
            return true;
        }

        var httpsCandidate = $"https://{value}";
        if (Uri.TryCreate(httpsCandidate, UriKind.Absolute, out var httpsUri) &&
            IsPlausibleHost(httpsUri.Host))
        {
            normalizedUrl = httpsCandidate;
            uri = httpsUri;
            return true;
        }

        normalizedUrl = string.Empty;
        uri = null!;
        return false;
    }

    private static bool IsExplicitAbsoluteUrl(string value, Uri uri)
    {
        if (value.Contains("://", StringComparison.Ordinal))
        {
            return !string.IsNullOrWhiteSpace(uri.Scheme);
        }

        return uri.Scheme.Equals("mailto", StringComparison.OrdinalIgnoreCase) ||
               uri.Scheme.Equals("file", StringComparison.OrdinalIgnoreCase) ||
               uri.Scheme.Equals("chrome", StringComparison.OrdinalIgnoreCase) ||
               uri.Scheme.Equals("chrome-extension", StringComparison.OrdinalIgnoreCase) ||
               uri.Scheme.Equals("javascript", StringComparison.OrdinalIgnoreCase) ||
               uri.Scheme.Equals("about", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPlausibleHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        return host.Contains('.', StringComparison.Ordinal) ||
               host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
               IPAddress.TryParse(host, out _);
    }

    private static string DeriveName(Uri uri, string normalizedUrl)
    {
        if (!string.IsNullOrWhiteSpace(uri.Host))
        {
            var host = uri.IdnHost;
            return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
                ? host[4..]
                : host;
        }

        if (uri.IsFile)
        {
            var fileName = Path.GetFileName(uri.LocalPath);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return fileName;
            }
        }

        return normalizedUrl;
    }
}
