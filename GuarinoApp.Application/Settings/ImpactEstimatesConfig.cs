namespace GuarinoApp.Application.Settings
    {
    /// <summary>
    /// Estimaciones de pérdida de cosecha por tipo de evento (valores en %)
    /// </summary>
    public class ImpactEstimatesConfig
        {
        public double HeladaSeveraPerdida { get; set; }
        public double HeladaPerdida { get; set; }
        public double LluviaExcesivaPerdida { get; set; }
        public double LluviaMuyExcesivaPerdida { get; set; }
        public double VientoFuertePerdida { get; set; }
        public double CalorExtremoPerdida { get; set; }
        }
    }
