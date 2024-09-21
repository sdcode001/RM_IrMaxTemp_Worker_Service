namespace eye.analytics.irmaxtemp.Models
{
    public class AnalyticInput
    {
        public int asset_detail_id { get; set; }
        public double latest_value { get; set; }

        public AnalyticInput()
        {

        }
        public AnalyticInput(int asset_detail_id, double latest_value)
        {
            this.asset_detail_id = asset_detail_id;
            this.latest_value = latest_value;
        }
    }
}