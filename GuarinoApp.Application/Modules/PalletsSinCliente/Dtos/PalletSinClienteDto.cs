namespace GuarinoApp.Application.Modules.PalletsSinCliente.Dtos
    {
    public class PalletSinClienteDto
        {
        public int Id { get; set; }
        public string? Codigo { get; set; }
        public string? Variedad { get; set; }
        public string? Estado { get; set; }
        public DateTime? Fecha { get; set; }

        // Campos requeridos por el SELECT en el handler
        public string? Contramarca { get; set; }
        public string? Calibre { get; set; }

        // Mantener Cliente por compatibilidad si alguna vista lo usa
        public string? Cliente { get; set; }
        }
    }
