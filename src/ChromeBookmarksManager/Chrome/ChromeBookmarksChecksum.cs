using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Chrome;

public static class ChromeBookmarksChecksum
{
    private static readonly UTF8Encoding StrictUtf8 =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static ChromeBookmarksChecksums Compute(BookmarkDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        AppendNode(document.Roots.BookmarkBar, md5, sha256);
        AppendNode(document.Roots.Other, md5, sha256);
        AppendNode(document.Roots.Synced, md5, sha256);

        return new ChromeBookmarksChecksums(
            Convert.ToHexString(md5.GetHashAndReset()).ToLowerInvariant(),
            Convert.ToHexString(sha256.GetHashAndReset()).ToLowerInvariant());
    }

    private static void AppendNode(
        BookmarkNode node,
        IncrementalHash md5,
        IncrementalHash sha256)
    {
        AppendUtf8(node.Id, md5, sha256);
        AppendUtf16LittleEndian(node.Name, md5, sha256);

        switch (node)
        {
            case BookmarkUrl bookmark:
                AppendUtf8("url", md5, sha256);
                AppendUtf8(bookmark.Url, md5, sha256);
                break;

            case BookmarkFolder folder:
                AppendUtf8("folder", md5, sha256);
                foreach (var child in folder.Children)
                {
                    AppendNode(child, md5, sha256);
                }

                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported bookmark node type {node.GetType().FullName}.");
        }
    }

    private static void AppendUtf8(
        string value,
        IncrementalHash md5,
        IncrementalHash sha256)
    {
        var byteCount = StrictUtf8.GetByteCount(value);
        if (byteCount == 0)
        {
            return;
        }

        var buffer = ArrayPool<byte>.Shared.Rent(byteCount);
        try
        {
            var written = StrictUtf8.GetBytes(
                value.AsSpan(),
                buffer.AsSpan(0, byteCount));
            var bytes = buffer.AsSpan(0, written);
            md5.AppendData(bytes);
            sha256.AppendData(bytes);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void AppendUtf16LittleEndian(
        string value,
        IncrementalHash md5,
        IncrementalHash sha256)
    {
        if (value.Length == 0)
        {
            return;
        }

        var byteCount = checked(value.Length * sizeof(char));
        var buffer = ArrayPool<byte>.Shared.Rent(byteCount);
        try
        {
            var offset = 0;
            foreach (var codeUnit in value)
            {
                buffer[offset++] = (byte)codeUnit;
                buffer[offset++] = (byte)(codeUnit >> 8);
            }

            var bytes = buffer.AsSpan(0, byteCount);
            md5.AppendData(bytes);
            sha256.AppendData(bytes);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
