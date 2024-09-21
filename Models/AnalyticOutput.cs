using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace eye.analytics.irmaxtemp.Models
{

    [Table("asset_details")]
    public class AnalyticOutputDB
    {
        [Column("AssetId")]
        public int AssetId { get; set; }

        [Column("AssetDetailId")]
        public int AssetDetailId { get; set; }

        [Column("LatestValue")]
        public double LatestValue { get; set; }
    }


    public class AnalyticOutput
    {
        public int asset_id { get; set; }

        public int output_tag_id { get; set; }

        public double max_temp { get; set; }

        //public AnalyticOutput(int asset_id, double max_temp)
        //{
        //    this.asset_id = asset_id;
        //    this.max_temp = max_temp;
        //}

    }
}