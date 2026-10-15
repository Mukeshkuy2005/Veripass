namespace VeriPass.Application.Common.Interfaces;

public class DocumentExtractionResult
{
    public bool Success { get; set; }
    public decimal Confidence { get; set; }
    public Dictionary<string, string> Fields { get; set; } = new();
    public string RawText { get; set; } = string.Empty;
    public bool HasTamperingSigns { get; set; }
    public string? TamperingDetails { get; set; }
}

public interface IDocumentIntelligenceService
{
    Task<DocumentExtractionResult> ExtractAndAnalyzeAsync(Stream documentStream, string documentType, CancellationToken cancellationToken = default);
}
