namespace GuarinoApp.Application.Modules.GraficaProcesos.Dtos
    {
    public class GraficaProcesosDto
        {
        public string Loteroda { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public int IdEspecie { get; set; }
        public string Especie { get; set; } = string.Empty;
        public string Variedad { get; set; } = string.Empty;
        public int IdVariedad { get; set; }
        public int Calibradora { get; set; }
        public int DcteSincera { get; set; }
        public int Volcado { get; set; }
        public int Hscal { get; set; }
        public int Hspar { get; set; }
        public int Ritmo1 { get; set; }
        public int Ritmo2 { get; set; }
        public string Observacion { get; set; } = string.Empty;
        }
    }
