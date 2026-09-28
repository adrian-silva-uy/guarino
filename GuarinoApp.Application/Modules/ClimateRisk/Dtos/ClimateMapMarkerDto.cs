namespace GuarinoApp.Application.Modules.ClimateRisk.Dtos
    {
    public class ClimateMapMarkerDto
        {
        public double Lat { get; set; }
        public double Lng { get; set; }
        public string Label { get; set; } = string.Empty;
        public string? Info { get; set; }
        public string? Color { get; set; }
        }
    }
