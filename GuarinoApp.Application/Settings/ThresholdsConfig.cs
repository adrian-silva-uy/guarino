namespace GuarinoApp.Application.Settings
    {
    /// <summary>
    /// Umbrales meteorológicos para clasificación de eventos climáticos
    /// </summary>
    public class ThresholdsConfig
        {
        public double HeladaTempMin { get; set; }
        public double HeladaTempSevera { get; set; }
        public double LluviaExcesivaMm { get; set; }
        public double LluviaMuyExcesivaMm { get; set; }
        public double VientoFuerteKmh { get; set; }
        public double VientoFuerteKmhCritico { get; set; }
        public double CalorExtremoTempMax { get; set; }
        }
    }
