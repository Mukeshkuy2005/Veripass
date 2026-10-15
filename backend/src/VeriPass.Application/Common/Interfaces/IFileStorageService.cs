namespace VeriPass.Application.Common.Interfaces;

public interface IFileStorageService
{
    Task<(string storageKey, string sha256Hash, long fileSize)> UploadAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task<Stream?> DownloadAsync(string storageKey, CancellationToken cancellationToken = default);
    Task<string> GetSignedDownloadUrlAsync(string storageKey, TimeSpan expiry, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
