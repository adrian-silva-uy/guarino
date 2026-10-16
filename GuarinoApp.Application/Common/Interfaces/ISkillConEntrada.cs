namespace GuarinoApp.Application.Common.Interfaces;

/// <summary>
/// Skill que además recibe el mensaje original del usuario: la usan las
/// acciones que dependen de lo que el usuario pegó en el chat (p. ej.
/// importar contactos desde una lista de WhatsApp o un CSV), a diferencia de
/// las skills de consulta, que son ciegas respecto del prompt.
/// </summary>
public interface ISkillConEntrada : ISkill
{
    /// <summary>
    /// Ejecuta la skill procesando la entrada del usuario (texto completo del
    /// mensaje que activó la intención).
    /// </summary>
    Task<SkillOutput> ExecuteAsync(string entrada, CancellationToken cancellationToken = default);
}
