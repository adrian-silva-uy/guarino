namespace GuarinoApp.Domain.Entities
    {
    public class StockVariedad
        {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public int Mes { get; set; }
        public int Año { get; set; }
        public string? Variedad { get; set; }
        public int Cantidad { get; set; }
        public string? Color { get; set; }
        }
    }
