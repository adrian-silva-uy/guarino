namespace GuarinoApp.Domain.Entities
    {
    public class VolcadoHistorico
        {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public int Mes { get; set; }
        public int Año { get; set; }
        public string? Variedad { get; set; }
        public decimal KgsVolcados { get; set; }
        public int Bins { get; set; }
        public string? Productor { get; set; }
        public string? Zafra { get; set; }
        }
    }
