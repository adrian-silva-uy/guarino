namespace GuarinoApp.Application.Common.Interfaces
    {
    public interface IClimateService
        {
        Task DetectarYRegistrarEventosAsync(CancellationToken cancellationToken);
        }
    }
