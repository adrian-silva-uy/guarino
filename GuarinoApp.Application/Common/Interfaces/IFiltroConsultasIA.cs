namespace GuarinoApp.Application.Common.Interfaces;

/// <summary>
/// Filtro de consultas del Asistente IA por rol (capa 1 de defensa). Clasifica
/// la pregunta por temas según la matriz real de módulos de la app (Contaduría
/// y Facturación → Admin/Gerencia/Contador; Usuarios/Auditoría/Contactos/
/// Gerencia → Admin/Gerencia; operación diaria → todos) y, si el rol del
/// usuario no puede consultar ese tema, devuelve un mensaje de rechazo
/// inteligente —con temas permitidos sugeridos para su perfil— ya registrado
/// en Auditoría como "ConsultaIArestringida". Devuelve null cuando la consulta
/// está permitida. Se invoca en el único punto de entrada del chat, antes de
/// la caché, para que un rechazo nunca se cachee ni llegue al modelo.
/// </summary>
public interface IFiltroConsultasIA
{
    Task<string?> EvaluarAsync(string pregunta, CancellationToken cancellationToken = default);
}
