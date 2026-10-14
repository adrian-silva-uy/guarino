using GuarinoApp.Application.Common.Models;

namespace GuarinoApp.Application.Common.Interfaces
    {
    public interface IEmailService
        {
        Task SendEmailAsync(string to, string subject, string body, byte[]? attachment = null, string attachmentName = "", string? from = null, string? fromName = null, string? cc = null);
        Task<PdfResult> CreatePdfAsync(byte[] pdfBytes, string? outputDirectory = null, string? fileName = null);
        Task<EmailResult> SendEmailWithTemplateAndAttachmentAsync(
            string to,
            string subject,
            string templateFilePath,
            IDictionary<string, string>? templatePlaceholders,
            string? attachmentFilePath);
        }
    }
