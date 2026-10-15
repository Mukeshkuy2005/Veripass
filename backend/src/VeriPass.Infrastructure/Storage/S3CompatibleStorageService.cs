using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using VeriPass.Application.Common.Interfaces;

namespace VeriPass.Infrastructure.Storage;

public class S3CompatibleStorageService : IFileStorageService
{
    private readonly string _baseStoragePath;
    private readonly ISignedUrlService _signedUrlService;

    public S3CompatibleStorageService(IConfiguration configuration, ISignedUrlService signedUrlService)
    {
        _signedUrlService = signedUrlService;
        var configuredPath = configuration["Storage:LocalPath"];
        _baseStoragePath = !string.IsNullOrEmpty(configuredPath)
            ? configuredPath
            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "veripass_storage");

        if (!Directory.Exists(_baseStoragePath))
        {
            Directory.CreateDirectory(_baseStoragePath);
        }
    }

    public async Task<(string storageKey, string sha256Hash, long fileSize)> UploadAsync(
        Stream stream, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        var fileExtension = Path.GetExtension(fileName);
        var uniqueKey = $"{Guid.NewGuid():N}{fileExtension}";
        var destinationPath = Path.Combine(_baseStoragePath, uniqueKey);

        using var destinationFileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var sha256 = SHA256.Create();

        if (stream.CanSeek)
            stream.Position = 0;

        var buffer = new byte[81920];
        int bytesRead;
        long totalBytes = 0;

        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
        {
            await destinationFileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
            totalBytes += bytesRead;
        }

        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        var hashString = Convert.ToHexString(sha256.Hash ?? Array.Empty<byte>());

        return (uniqueKey, hashString, totalBytes);
    }

    public Task<Stream?> DownloadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(_baseStoragePath, storageKey);
        if (!File.Exists(filePath))
        {
            return Task.FromResult<Stream?>(null);
        }

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }

    public Task<string> GetSignedDownloadUrlAsync(string storageKey, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_signedUrlService.GenerateSignedUrl(storageKey, expiry));
    }

    public Task<bool> DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(_baseStoragePath, storageKey);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
}
