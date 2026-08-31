using System.IO.Compression;
using System.Text;

namespace Infrastructure.Modules.ProjectTasks.CreateProjectTaskAttachment;

/// <summary>
/// Inspects attachment bytes without trusting the client-provided file name or content type.
/// </summary>
internal static class ProjectTaskAttachmentContentInspector
{
    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>
    /// Validates the actual stream length and format-specific content markers.
    /// </summary>
    public static async Task<string?> InspectAsync(
        Stream content,
        string extension,
        long declaredSizeBytes,
        CancellationToken cancellationToken)
    {
        if (!content.CanRead || !content.CanSeek)
        {
            return "Attachment content must be a readable, seekable stream";
        }

        var initialPosition = content.Position;
        try
        {
            if (content.Length - initialPosition != declaredSizeBytes)
            {
                return "Attachment size does not match its content";
            }

            var matchesDeclaredFormat = extension switch
            {
                ".pdf" => await StartsWithAsync(content, PdfSignature, cancellationToken),
                ".png" => await StartsWithAsync(content, PngSignature, cancellationToken),
                ".jpg" or ".jpeg" => await StartsWithAsync(content, JpegSignature, cancellationToken),
                ".docx" => await IsOpenXmlPackageAsync(content, "word/document.xml", cancellationToken),
                ".xlsx" => await IsOpenXmlPackageAsync(content, "xl/workbook.xml", cancellationToken),
                ".txt" => await IsUtf8TextAsync(content, cancellationToken),
                _ => false
            };

            return matchesDeclaredFormat
                ? null
                : "Attachment content does not match its declared format";
        }
        catch (IOException)
        {
            return "Attachment content could not be inspected";
        }
        catch (InvalidDataException)
        {
            return "Attachment content does not match its declared format";
        }
        finally
        {
            content.Position = initialPosition;
        }
    }

    private static async Task<bool> StartsWithAsync(
        Stream content,
        byte[] signature,
        CancellationToken cancellationToken)
    {
        var header = new byte[signature.Length];
        var bytesRead = await content.ReadAsync(header, cancellationToken);
        return bytesRead == signature.Length && header.AsSpan().SequenceEqual(signature);
    }

    private static async Task<bool> IsOpenXmlPackageAsync(
        Stream content,
        string requiredDocumentEntry,
        CancellationToken cancellationToken)
    {
        if (!await StartsWithAsync(content, ZipSignature, cancellationToken))
        {
            return false;
        }

        content.Position = 0;
        using var archive = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
        return archive.GetEntry("[Content_Types].xml") is not null
            && archive.GetEntry("_rels/.rels") is not null
            && archive.GetEntry(requiredDocumentEntry) is not null;
    }

    private static async Task<bool> IsUtf8TextAsync(
        Stream content,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(
            content,
            StrictUtf8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: true);
        var buffer = new char[4096];

        try
        {
            int charactersRead;
            while ((charactersRead = await reader.ReadAsync(buffer, cancellationToken)) > 0)
            {
                for (var index = 0; index < charactersRead; index++)
                {
                    var character = buffer[index];
                    if (char.IsControl(character)
                        && character is not '\t' and not '\r' and not '\n')
                    {
                        return false;
                    }
                }
            }

            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}