namespace GuarinoApp.Infrastructure.Services;

/// <summary>Resultado de la generación de prueba de la IA local (auto-reparación).</summary>
internal enum Diagnostico
    {
    Ok,                 // 2xx: la IA local funciona (falso positivo o ya se recuperó).
    ErrorDeGeneracion,  // 5xx: el API responde pero la generación falla ("colgado").
    SinConexion,        // conexión rechazada/sin host: proceso caído.
    ErrorDeClientes,    // 4xx: falta el modelo o hay un problema de configuración (NO se reinicia).
    Indeciso            // la verificación no pudo responder a tiempo (NO se reinicia).
    }
