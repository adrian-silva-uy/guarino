using GuarinoApp.Domain.Shared;

namespace GuarinoApp.Domain.Entities;

/// <summary>
/// Rol de acceso de la aplicación (tabla maestra). La lista de roles vive en
/// la base de datos: la validación de altas/ediciones y la UI se alimentan de
/// aquí, no de listas hardcodeadas — eliminar o renombrar un rol no deja
/// usuarios huérfanos y no puede divergir entre pantallas.
/// </summary>
public class Role : BaseEntity
{
    /// <summary>Nombre único del rol (Admin, Gerencia, Contador, Operador).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Indica si el rol puede asignarse a usuarios (alta o edición).</summary>
    public bool IsActive { get; set; } = true;
}
