using Microsoft.Extensions.Logging;

namespace VeriPass.Infrastructure.DocumentIntelligence;

public interface IOcrService
{
    Task<string> ExtractRawTextAsync(Stream documentStream, CancellationToken cancellationToken = default);
}

public class OcrService : IOcrService
{
    private readonly ILogger<OcrService> _logger;

    public OcrService(ILogger<OcrService> logger)
    {
        _logger = logger;
    }

    public async Task<string> ExtractRawTextAsync(Stream documentStream, CancellationToken cancellationToken = default)
    {
        using var memoryStream = new MemoryStream();
        if (documentStream.CanSeek)
            documentStream.Position = 0;

        await documentStream.CopyToAsync(memoryStream, cancellationToken);
        var bytes = memoryStream.ToArray();

        // Read printable UTF-8 or simulate OCR engine recognition
        var textContent = System.Text.Encoding.UTF8.GetString(bytes.Take(Math.Min(bytes.Length, 16384)).ToArray());

        _logger.LogDebug("Extracted {Length} characters via OCR engine.", textContent.Length);
        return textContent;
    }
}
