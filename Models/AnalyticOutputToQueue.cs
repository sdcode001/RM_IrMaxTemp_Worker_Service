using System.Text.Json.Serialization;

namespace eye.analytics.irmaxtemp.Models
{
    public class AnalyticOutputToQueue
    {
        [JsonPropertyName("tid")]
        public string TagId { get; set; }
        [JsonPropertyName("tn")]
        public string TagName { get; set; }
        [JsonPropertyName("tt")]
        public string TagType { get; set; }

        #region Alarm
        [JsonPropertyName("sid")]
        public string AlarmSettingId { get; set; }
        [JsonPropertyName("ack")]
        public string AlarmAck { get; set; }
        [JsonPropertyName("user")]
        public string AckBy { get; set; }
        [JsonPropertyName("comment")]
        public string Comments { get; set; }
        [JsonPropertyName("ack_time")]
        public string AckTime { get; set; }

        [JsonPropertyName("reset_time")]
        public string ResetTime { get; set; }
        [JsonPropertyName("pid")]
        public string PrimaryAssetDetailId { get; set; }
        #endregion

        [JsonPropertyName("val")]
        public string Value { get; set; }

        [JsonPropertyName("ts")]
        public string TimeStamp { get; set; }

        [JsonPropertyName("cid")]
        public string ClientId { get; set; }
    }
}