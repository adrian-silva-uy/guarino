namespace GuarinoApp.Infrastructure.Services
    {
    public interface IPdfGenerator
        {
        Task<byte[]> GeneratePalletPdfAsync(CancellationToken cancellationToken = default);
        Task<byte[]> GenerateDashboardPdfAsync(CancellationToken cancellationToken = default);
        Task<byte[]> GenerateFrutaPdfAsync(CancellationToken cancellationToken = default);
        }
    }
