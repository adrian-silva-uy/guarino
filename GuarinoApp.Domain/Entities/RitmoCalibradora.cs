namespace GuarinoApp.Domain.Entities
    {
    /// <summary>
    /// Representa filas de RITMOCALIBRADORA (solo lectura). Keyless para consultas con EF Core.
    /// </summary>
    public class RitmoCalibradora
        {
        public string? Loteroda { get; set; }
        public DateTime Fecha { get; set; }
        public int? IdEspecie { get; set; }
        public string? Especie { get; set; }
        public string? Variedad { get; set; }
        public int? IdVariedad { get; set; }
        public int? Calibradora { get; set; }
        public int? DcteSincera { get; set; }
        public int? Volcado { get; set; }
        public decimal? Hscal { get; set; }
        public decimal? Hspar { get; set; }
        public int? Ritmo1 { get; set; }
        public int? Ritmo2 { get; set; }
        public string? Observacion { get; set; }
        }
    }
