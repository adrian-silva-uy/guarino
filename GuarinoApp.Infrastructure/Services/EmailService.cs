using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Common.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace GuarinoApp.Infrastructure.Services
    {
    public class EmailService : IEmailService
        {
        private readonly ILogger<EmailService> _logger;
        private readonly EmailOptions _options;

        public EmailService(IOptions<EmailOptions> options, ILogger<EmailService> logger)
            {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            }

        public async Task SendEmailAsync(string to, string subject, string body, byte[]? attachment = null, string attachmentName = "", string? from = null, string? fromName = null, string? cc = null)
            {
            if (string.IsNullOrWhiteSpace(to))
                throw new ArgumentException("Destinatario (to) no proporcionado.", nameof(to));

            using var message = new MailMessage();
            message.From = ConstruirRemitente(from, fromName);
            message.To.Add(new MailAddress(to));
            message.Subject = subject ?? "(sin asunto)";
            message.Body = body ?? string.Empty;
            message.IsBodyHtml = false;

            // Copia al administrador: se omite si es la misma dirección que "para".
            if (!string.IsNullOrWhiteSpace(cc) && !string.Equals(cc, to, StringComparison.OrdinalIgnoreCase))
                message.CC.Add(new MailAddress(cc));

            if (attachment != null && attachment.Length > 0)
                {
                var ms = new MemoryStream(attachment);
                var attachmentFileName = string.IsNullOrWhiteSpace(attachmentName) ? "attachment" : attachmentName;
                var attachmentObj = new Attachment(ms, attachmentFileName);
                message.Attachments.Add(attachmentObj);
                // Attachment mantiene el stream hasta Send; no dispose inmediato.
                }

            using var client = BuildSmtpClient();

            await client.SendMailAsync(message).ConfigureAwait(false);
            _logger.LogInformation("Email enviado correctamente a {To}{Copia}", to,
                string.IsNullOrWhiteSpace(cc) || string.Equals(cc, to, StringComparison.OrdinalIgnoreCase)
                    ? string.Empty
                    : $" (copia: {cc})");
            }

        public async Task<EmailResult> SendEmailWithTemplateAndAttachmentAsync(string to, string subject, string templateFilePath, IDictionary<string, string>? templatePlaceholders, string? attachmentFilePath)
            {
            if (string.IsNullOrWhiteSpace(to))
                {
                return EmailResult.Fail("Destinatario (to) no proporcionado.");
                }

            if (templatePlaceholders == null)
                {
                templatePlaceholders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

            templatePlaceholders["To"] = to;

            string htmlBody;
            try
                {
                htmlBody = await LoadTemplateAsync(templateFilePath, templatePlaceholders).ConfigureAwait(false);
                }
            catch (Exception ex)
                {
                _logger.LogError(ex, "Error cargando template de email '{TemplateFilePath}'", templateFilePath);
                return EmailResult.Fail($"Error al cargar template: {ex.Message}");
                }

            try
                {
                var sendResult = await SendEmailAsyncInternal(to, subject, htmlBody, attachmentFilePath).ConfigureAwait(false);
                return sendResult;
                }
            catch (Exception ex)
                {
                _logger.LogError(ex, "Error enviando email a {To}. Inner: {Inner}", to, ex.InnerException?.Message);
                return EmailResult.Fail($"Error al enviar email: {ex.Message}" + (ex.InnerException != null ? $" | Detalle: {ex.InnerException.Message}" : string.Empty));
                }
            }

        public async Task<PdfResult> CreatePdfAsync(byte[] pdfBytes, string? outputDirectory = null, string? fileName = null)
            {
            if (pdfBytes == null || pdfBytes.Length == 0)
                {
                return PdfResult.Fail("El contenido del PDF está vacío.");
                }

            try
                {
                if (string.IsNullOrWhiteSpace(outputDirectory))
                    {
                    outputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GuarinoApp", "Pdfs");
                    }

                Directory.CreateDirectory(outputDirectory);

                var safeFileName = string.IsNullOrWhiteSpace(fileName)
                    ? $"document_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf"
                    : fileName;

                var filePath = Path.Combine(outputDirectory, safeFileName);

                await File.WriteAllBytesAsync(filePath, pdfBytes).ConfigureAwait(false);

                _logger.LogInformation("PDF creado correctamente en {FilePath}", filePath);

                return PdfResult.Success(filePath);
                }
            catch (Exception ex)
                {
                _logger.LogError(ex, "Error creando PDF");
                return PdfResult.Fail($"Error creando PDF: {ex.Message}");
                }
            }

        private async Task<string> LoadTemplateAsync(string templateFilePath, IDictionary<string, string> placeholders)
            {
            if (string.IsNullOrWhiteSpace(templateFilePath))
                throw new ArgumentException("templateFilePath no puede estar vacío.", nameof(templateFilePath));

            if (!File.Exists(templateFilePath))
                throw new FileNotFoundException("Template de email no encontrado.", templateFilePath);

            string template = await File.ReadAllTextAsync(templateFilePath, Encoding.UTF8).ConfigureAwait(false);

            foreach (var kv in placeholders)
                {
                var key = $"{{{kv.Key}}}";
                template = template.Replace(key, kv.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                }

            return template;
            }

        /// <summary>
        /// Dirección remitente: el parámetro recibido, luego Email:FromEmail,
        /// luego Email:SmtpUser (el buzón autenticado). Sin ninguna de las tres
        /// no se puede armar el remitente y se lanza un error claro.
        /// </summary>
        private string DireccionRemitente(string? desde) =>
            !string.IsNullOrWhiteSpace(desde) ? desde
            : !string.IsNullOrWhiteSpace(_options.FromEmail) ? _options.FromEmail
            : !string.IsNullOrWhiteSpace(_options.SmtpUser) ? _options.SmtpUser
            : throw new InvalidOperationException("No hay remitente configurado: defina Email:FromEmail o Email:SmtpUser.");

        private MailAddress ConstruirRemitente(string? desde, string? nombre)
            {
            var direccion = DireccionRemitente(desde);
            var displayName = nombre ?? _options.FromName ?? string.Empty;
            return string.IsNullOrEmpty(displayName)
                ? new MailAddress(direccion)
                : new MailAddress(direccion, displayName);
            }

        /// <summary>
        /// Construye el From/Reply-To respetando UseCompanyMailboxForSending, UseUserNameInFromDisplay
        /// y UseReplyToUser. userDisplayName / userReplyToEmail permiten pasar datos del usuario logueado
        /// cuando se llame desde un contexto autenticado (hoy no se usan porque los métodos públicos
        /// no reciben esos datos; quedan preparados para cuando se necesiten).
        /// </summary>
        private void ApplyFromAndReplyTo(MailMessage message, string? userDisplayName = null, string? userReplyToEmail = null)
            {
            if (_options.UseCompanyMailboxForSending)
                {
                var displayName = _options.UseUserNameInFromDisplay && !string.IsNullOrWhiteSpace(userDisplayName)
                    ? $"{userDisplayName} via {_options.FromName}"
                    : _options.FromName;

                message.From = ConstruirRemitente(null, displayName);

                if (_options.UseReplyToUser && !string.IsNullOrWhiteSpace(userReplyToEmail))
                    {
                    message.ReplyToList.Add(new MailAddress(userReplyToEmail));
                    }
                }
            else
                {
                message.From = ConstruirRemitente(null, _options.FromName);
                }
            }

        private SmtpClient BuildSmtpClient()
            {
            var client = new SmtpClient
                {
                Host = _options.SmtpServer,
                Port = _options.SmtpPort,
                EnableSsl = _options.EnableSsl,
                UseDefaultCredentials = _options.UseDefaultCredentials
                };
            _logger.LogDebug("SMTP -> Host={Host} Port={Port} SmtpUser={SmtpUser} PasswordLength={PasswordLength} UseDefaultCredentials={UseDefaultCredentials}",
            _options.SmtpServer, _options.SmtpPort,
            string.IsNullOrWhiteSpace(_options.SmtpUser) ? _options.FromEmail : _options.SmtpUser,
            _options.Password?.Length ?? 0,
            _options.UseDefaultCredentials);
            if (!_options.UseDefaultCredentials)
                {
                if (_options.UseOAuth2)
                    {
                    // OAuth2 requiere un mecanismo XOAUTH2 que System.Net.Mail.SmtpClient no soporta
                    // de forma nativa. Si se activa UseOAuth2, este código NO va a autenticar correctamente
                    // sin una librería adicional (p. ej. MailKit) que arme el comando AUTH XOAUTH2 a mano.
                    _logger.LogWarning("UseOAuth2 está activado pero SmtpClient no soporta XOAUTH2 nativamente. " +
                        "Considerar migrar a MailKit para autenticación OAuth2 real.");
                    }

                var smtpUser = string.IsNullOrWhiteSpace(_options.SmtpUser) ? _options.FromEmail : _options.SmtpUser;
                client.Credentials = string.IsNullOrWhiteSpace(smtpUser)
                    ? null
                    : new NetworkCredential(smtpUser, _options.Password);
                }

            return client;
            }

        private async Task<EmailResult> SendEmailAsyncInternal(string to, string subject, string htmlBody, string? attachmentFilePath)
            {
            using var message = new MailMessage();
            ApplyFromAndReplyTo(message);

            if (!string.IsNullOrWhiteSpace(_options.EnvelopeFrom))
                {
                // El Envelope-From (MAIL FROM SMTP) puede diferir del header From visible.
                // System.Net.Mail no expone esto directamente; Sender es lo más cercano disponible.
                message.Sender = new MailAddress(_options.EnvelopeFrom);
                }

            message.To.Add(new MailAddress(to));
            message.Subject = subject ?? "(sin asunto)";
            message.Body = htmlBody ?? string.Empty;
            message.IsBodyHtml = true;

            if (!string.IsNullOrWhiteSpace(attachmentFilePath) && File.Exists(attachmentFilePath))
                {
                var attachment = new Attachment(attachmentFilePath);
                message.Attachments.Add(attachment);
                }
            else if (!string.IsNullOrWhiteSpace(attachmentFilePath))
                {
                _logger.LogWarning("El archivo adjunto especificado no existe: {AttachmentFilePath}", attachmentFilePath);
                }

            using var client = BuildSmtpClient();

            try
                {
                await client.SendMailAsync(message).ConfigureAwait(false);
                _logger.LogInformation("Email enviado correctamente a {To}", to);
                return EmailResult.Success("Email enviado correctamente.", attachmentFilePath);
                }
            catch (Exception ex)
                {
                _logger.LogError(ex, "Error enviando email a {To}. Inner: {Inner}", to, ex.InnerException?.Message);
                return EmailResult.Fail($"Error enviando email: {ex.Message}" + (ex.InnerException != null ? $" | Detalle: {ex.InnerException.Message}" : string.Empty));
                }
            }
        }
    }
