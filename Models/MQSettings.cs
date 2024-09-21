using System;

namespace eye.analytics.irmaxtemp.Models
{
    public class CommunicatorMQSettings
    {
        public MQSettings irMaxTempAnalyticQ { get; set; }
        public MQSettings alarmq { get; set; }
        public MQSettings commandq { get; set; }
        public MQSettings snapshotq { get; set; }
        public MQSettings calcq { get; set; }
        public MQSettings dblogq { get; set; }
        public MQSettings uiq { get; set; }
        public MQSettings irMaxTempQ {  get; set; }
    }

    public class MQSettings
    {
        public string Server { get; set; }
        public string Port { get; set; }
        public bool IsEnable { get; set; }
        public int? MaxReads { get; set; }
        public string ListName { get; set; }
    }
}


