using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Common;

/// <summary>
/// Helpers for creating and reading Word (.docx) documents.
/// </summary>
public static class WordDocumentHelper
{
    /// <summary>
    /// Creates a .docx file with a title and narrative body text.
    /// Each paragraph in the narrative (split by double-newline) becomes a separate Word paragraph.
    /// </summary>
    public static void CreateDocument(string filePath, string title, string narrativeText)
    {
        using var document = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document);

        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = new Body();

        // Add title paragraph with Heading1 style
        var titleParagraph = new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "Heading1" }),
            new Run(
                new RunProperties(new Bold(), new FontSize { Val = "36" }),
                new Text(title)));
        body.Append(titleParagraph);

        // Add a blank line after title
        body.Append(new Paragraph());

        // Split the narrative into paragraphs by double-newline
        var paragraphs = narrativeText.Split(
            ["\r\n\r\n", "\n\n"],
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var para in paragraphs)
        {
            var trimmed = para.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            // Check if this looks like a heading (single short line, no period at end)
            var isHeading = !trimmed.Contains('\n')
                && trimmed.Length < 100
                && !trimmed.EndsWith('.')
                && !trimmed.EndsWith(',')
                && char.IsUpper(trimmed[0]);

            if (isHeading && trimmed.Length < 80)
            {
                var headingParagraph = new Paragraph(
                    new ParagraphProperties(new ParagraphStyleId { Val = "Heading2" }),
                    new Run(
                        new RunProperties(new Bold(), new FontSize { Val = "28" }),
                        new Text(trimmed)));
                body.Append(headingParagraph);
            }
            else
            {
                // Regular body paragraph — preserve internal line breaks
                var lines = trimmed.Split(["\r\n", "\n"], StringSplitOptions.None);
                var bodyParagraph = new Paragraph();
                for (int i = 0; i < lines.Length; i++)
                {
                    bodyParagraph.Append(new Run(new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve }));
                    if (i < lines.Length - 1)
                    {
                        bodyParagraph.Append(new Run(new Break()));
                    }
                }
                body.Append(bodyParagraph);
            }
        }

        mainPart.Document.Append(body);
        mainPart.Document.Save();
    }

    /// <summary>
    /// Extracts all text content from a .docx file, returning it as a single string.
    /// Paragraphs are separated by newlines.
    /// </summary>
    public static string ExtractText(string filePath)
    {
        // Open with FileShare.ReadWrite to avoid conflicts if the file is open in Word.
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var document = WordprocessingDocument.Open(stream, false);
        var body = document.MainDocumentPart?.Document?.Body;

        if (body is null)
        {
            return string.Empty;
        }

        var paragraphs = new List<string>();

        foreach (var paragraph in body.Elements<Paragraph>())
        {
            var text = paragraph.InnerText;
            paragraphs.Add(text);
        }

        return string.Join(Environment.NewLine, paragraphs);
    }
}
