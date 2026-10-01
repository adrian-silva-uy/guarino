namespace GuarinoApp.Domain.Enums;
public enum EstadoPallet
    {
    Despachado = 1,
    EnFrio = 2,
    PreFrio = 3,
    Rearmado = 4,
    // 'B' del SQL legacy (Baja/Borrador): existía como texto en los datos
    // históricos pero no como miembro del enum, lo que obligaba a filtrar por
    // la columna duplicada EstadoTexto. Se agrega al final para no alterar
    // los valores existentes.
    Bajado = 5
    }
