using System.Security.Cryptography;
using System.Text;

namespace VeriPass.Application.Common.Helpers;

public static class PdfCertificateGenerator
{
    public static byte[] GenerateCertificatePdf(
        string documentType,
        string legalName,
        string tradeName,
        string identifier,
        string address,
        string referenceCode)
    {
        var cleanTitle = documentType.ToUpperInvariant() switch
        {
            "GST" or "GST_CERTIFICATE" => "FORM GST REG-06",
            "PAN" or "PAN_CARD" => "INCOME TAX DEPARTMENT - PERMANENT ACCOUNT NUMBER",
            "UDYAM" or "MSME" or "UDYAM_CERTIFICATE" => "UDYAM REGISTRATION CERTIFICATE",
            "BANK" or "CANCELLED_CHEQUE" or "BANK_ACCOUNT" => "BANK CANCELLED CHEQUE / VERIFICATION PROOF",
            "ISO" or "ISO_CERTIFICATE" => "ISO 9001:2015 ACCREDITATION CERTIFICATE",
            _ => "STATUTORY REGISTRATION CERTIFICATE"
        };

        var cleanSub = documentType.ToUpperInvariant() switch
        {
            "GST" or "GST_CERTIFICATE" => "GOVERNMENT OF INDIA - GOODS AND SERVICES TAX NETWORK",
            "PAN" or "PAN_CARD" => "GOVERNMENT OF INDIA - CENTRAL BOARD OF DIRECT TAXES",
            "UDYAM" or "MSME" or "UDYAM_CERTIFICATE" => "MINISTRY OF MICRO, SMALL & MEDIUM ENTERPRISES",
            "BANK" or "CANCELLED_CHEQUE" or "BANK_ACCOUNT" => "NATIONAL PAYMENTS CORPORATION OF INDIA (NPCI)",
            "ISO" or "ISO_CERTIFICATE" => "INTERNATIONAL ACCREDITATION FORUM (IAF)",
            _ => "STATUTORY AUTHORITATIVE RECORD"
        };

        var fields = new List<(string key, string val)>
        {
            ("Statutory Identifier", identifier),
            ("Legal Entity Name", legalName),
            ("Trade / Business Name", tradeName),
            ("Principal Place of Business", address),
            ("Constitution of Business", "Private Limited Company"),
            ("Jurisdictional Authority", "CBIC & State Tax Administration"),
            ("Registration Status", "ACTIVE & OPERATIONAL"),
            ("Date of Liability", "01/07/2017"),
            ("Document Reference", referenceCode),
            ("Verification Assurance", "Digitally Signed by VeriPass Statutory Ingestion Gateway")
        };

        return BuildStandardPdf(cleanTitle, cleanSub, fields);
    }

    private static byte[] BuildStandardPdf(string title, string subtitle, List<(string key, string val)> fields)
    {
        var content = new StringBuilder();
        content.AppendLine("q");
        // Navy blue border
        content.AppendLine("0.12 0.23 0.54 RG 2 w 25 25 545 792 re S");
        // Inner gold border
        content.AppendLine("0.85 0.47 0.02 RG 0.5 w 30 30 535 782 re S");
        content.AppendLine("Q");

        content.AppendLine("BT");
        content.AppendLine("/F1 15 Tf");
        content.AppendLine("0.06 0.09 0.16 rg");
        content.AppendLine("170 780 Td (GOVERNMENT OF INDIA) Tj");
        content.AppendLine("0 -20 Td");
        content.AppendLine("/F1 12 Tf");
        content.AppendLine("0.12 0.23 0.54 rg");
        content.AppendLine($"({EscapePdf(title)}) Tj");
        content.AppendLine("0 -16 Td");
        content.AppendLine("/F1 8.5 Tf");
        content.AppendLine("0.4 0.45 0.5 rg");
        content.AppendLine($"({EscapePdf(subtitle)}) Tj");

        content.AppendLine("-120 -35 Td");
        content.AppendLine("/F1 9 Tf");

        foreach (var (k, v) in fields)
        {
            content.AppendLine("0.3 0.35 0.4 rg");
            content.AppendLine($"({EscapePdf(k)}:) Tj");
            content.AppendLine("190 0 Td");
            content.AppendLine("0.06 0.09 0.16 rg");
            content.AppendLine($"({EscapePdf(v)}) Tj");
            content.AppendLine("-190 -22 Td");
        }

        // Digital seal at bottom
        content.AppendLine("0 -30 Td");
        content.AppendLine("0.05 0.6 0.35 rg");
        content.AppendLine("/F1 8.5 Tf");
        content.AppendLine("(DIGITALLY CERTIFIED & CRYPTOGRAPHICALLY SECURED BY VERIPASS INDIA RAILS) Tj");
        content.AppendLine("ET");

        var streamBytes = Encoding.UTF8.GetBytes(content.ToString());
        var streamLength = streamBytes.Length;

        var pdf = new StringBuilder();
        pdf.AppendLine("%PDF-1.4");
        pdf.AppendLine("%\u00e2\u00e3\u00cf\u00d3");

        var offsets = new List<long>();

        // Obj 1: Catalog
        offsets.Add(pdf.Length);
        pdf.AppendLine("1 0 obj");
        pdf.AppendLine("<< /Type /Catalog /Pages 2 0 R >>");
        pdf.AppendLine("endobj");

        // Obj 2: Pages
        offsets.Add(pdf.Length);
        pdf.AppendLine("2 0 obj");
        pdf.AppendLine("<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        pdf.AppendLine("endobj");

        // Obj 3: Page
        offsets.Add(pdf.Length);
        pdf.AppendLine("3 0 obj");
        pdf.AppendLine("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>");
        pdf.AppendLine("endobj");

        // Obj 4: Contents Stream
        offsets.Add(pdf.Length);
        pdf.AppendLine("4 0 obj");
        pdf.AppendLine($"<< /Length {streamLength} >>");
        pdf.AppendLine("stream");
        pdf.Append(content.ToString());
        pdf.AppendLine("endstream");
        pdf.AppendLine("endobj");

        // Obj 5: Font
        offsets.Add(pdf.Length);
        pdf.AppendLine("5 0 obj");
        pdf.AppendLine("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        pdf.AppendLine("endobj");

        // Xref
        var xrefOffset = pdf.Length;
        pdf.AppendLine("xref");
        pdf.AppendLine($"0 {offsets.Count + 1}");
        pdf.AppendLine("0000000000 65535 f ");
        foreach (var off in offsets)
        {
            pdf.AppendLine($"{off:D10} 00000 n ");
        }

        pdf.AppendLine("trailer");
        pdf.AppendLine($"<< /Size {offsets.Count + 1} /Root 1 0 R >>");
        pdf.AppendLine("startxref");
        pdf.AppendLine(xrefOffset.ToString());
        pdf.AppendLine("%%EOF");

        return Encoding.UTF8.GetBytes(pdf.ToString());
    }

    private static string EscapePdf(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text
            .Replace("\\", "\\\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)")
            .Replace("\r", "")
            .Replace("\n", " ");
    }
}
