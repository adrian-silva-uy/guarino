namespace GuarinoApp.Application.Common.Models
    {
    public record EmailResult(bool IsSuccess, string Message, string? AttachmentPath)
        {
        public static EmailResult Success(string message, string? attachmentPath = null) => new(true, message, attachmentPath);
        public static EmailResult Fail(string message) => new(false, message, null);
        }

    public record PdfResult(bool IsSuccess, string? Path, string Message)
        {
        public static PdfResult Success(string? path) => new(true, path, "PDF creado correctamente.");
        public static PdfResult Fail(string message) => new(false, null, message);
        }
    }
