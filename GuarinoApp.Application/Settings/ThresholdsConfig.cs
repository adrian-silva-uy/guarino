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

        /// <summary>
        /// Código meteorológico WMO mínimo (Open-Meteo weather_code) que clasifica la
        /// hora como granizo. 96 = tormenta con granizo leve, 99 = granizo intenso.
        /// </summary>
        public double GranizoWeatherCodeMin { get; set; }

        /// <summary>
        /// Código meteorológico WMO a partir del cual el granizo se considera severo.
        /// </summary>
        public double GranizoWeatherCodeSevera { get; set; }
        }
    }
