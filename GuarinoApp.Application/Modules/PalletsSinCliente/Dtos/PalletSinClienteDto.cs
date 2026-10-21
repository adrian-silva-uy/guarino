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

        // Sin propiedad Cliente: la página es "pallets SIN cliente", la columna
        // quedaba siempre vacía en la grilla (el handler nunca la mapeaba) y
        // solo confundía. La página renderiza columnas por reflexión, así que
        // al quitarla acá desaparece de la vista.
        }
    }
