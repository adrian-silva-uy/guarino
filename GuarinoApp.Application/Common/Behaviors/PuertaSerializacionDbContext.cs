namespace GuarinoApp.Application.Common.Behaviors;

/// <summary>
/// Serializa las operaciones sobre el DbContext de UN circuito Blazor.
///
/// El DbContext es Scoped (uno por circuito): cuando la página anterior dejó
/// una consulta en vuelo (o dos flujos de la misma página se cruzan — carga
/// inicial + clic del usuario) y entra otra operación, EF lanza
/// "A second operation was started on this context instance". Esto se vio en
/// la prueba de fuego: Auditoría mostraba "0 registros" porque su consulta
/// reventó contra una operación solapada.
///
/// La puerta encola las operaciones del circuito para que nunca se solapen.
/// Entre circuitos/usuarios NO hay bloqueo: cada circuito resuelve su propia
/// instancia (Scoped).
///
/// Reentrancia: un handler que envía otro request MediatR por dentro
/// (p. ej. GetImpactoClimaticoEnProyeccionQuery) no debe volver a esperar la
/// puerta que él mismo sostiene, o se produciría deadlock. El AsyncLocal marca
/// la cadena de ejecución actual y solo esa cadena se ahorra la espera; los
/// flujos concurrentes siguen encolándose.
///
/// Solo MediatR la adquiere (vía SerializacionDbContextBehavior). Los servicios
/// invocados desde handlers (auditoría, dashboard, etc.) usan scope propio, por
/// lo que nunca piden la puerta dos veces.
/// </summary>
public sealed class PuertaSerializacionDbContext : IDisposable
{
    private static readonly AsyncLocal<bool> DentroDeLaPuerta = new();
    private readonly SemaphoreSlim _sem = new(1, 1);

    public async Task<T> EjecutarAsync<T>(Func<Task<T>> operacion)
    {
        if (DentroDeLaPuerta.Value)
            return await operacion().ConfigureAwait(false);

        await _sem.WaitAsync().ConfigureAwait(false);
        DentroDeLaPuerta.Value = true;
        try
        {
            return await operacion().ConfigureAwait(false);
        }
        finally
        {
            DentroDeLaPuerta.Value = false;
            try
            {
                _sem.Release();
            }
            catch (ObjectDisposedException)
            {
                // El scope del circuito se dispuso con la operación todavía en
                // vuelo (navegación o cierre de pestaña): la puerta ya no existe
                // y no hay nada que liberar. Es una carrera de apagado, no un
                // error de la aplicación.
            }
        }
    }

    public void Dispose() => _sem.Dispose();
}
