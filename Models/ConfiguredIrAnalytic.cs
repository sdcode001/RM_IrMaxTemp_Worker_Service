using eye.analytics.irmaxtemp.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace eye.analytics.irmaxtemp.Models
{
    [Table("ir_thermography_configuration")]
    public class ConfiguredAnalyticDB
    {
        [Column("AssetId")]
        public int AssetId { get; set; }

        [Column("AssetDetailId")]
        public int AssetDetailId { get; set; }

        [Column("LatestValue ")]
        public double LatestValue { get; set; }
    }

    public class ConfiguredIrAnalytic
    {
        public int asset_id { get; set; }

        public List<AnalyticInput> analyticInputs { get; set; }

        public AnalyticOutput analyticOutput { get; set; }
    }

}