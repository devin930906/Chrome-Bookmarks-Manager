using System.IO;

namespace ChromeBookmarksManager.Infrastructure.Persistence;

public class BookmarkFileSystem
{
    public virtual Stream CreateWriteStream(string path)
    {
        return new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 128 * 1024,
            options: FileOptions.Asynchronous |
                     FileOptions.SequentialScan |
                     FileOptions.WriteThrough);
    }

    public virtual void Copy(
        string sourceFileName,
        string destinationFileName)
    {
        File.Copy(
            sourceFileName,
            destinationFileName,
            overwrite: false);
    }

    public virtual void Replace(
        string sourceFileName,
        string destinationFileName)
    {
        File.Replace(
            sourceFileName,
            destinationFileName,
            destinationBackupFileName: null,
            ignoreMetadataErrors: false);
    }

    public virtual bool Exists(string path) =>
        File.Exists(path);

    public virtual void Delete(string path) =>
        File.Delete(path);
}
