using System;
using System.Threading.Tasks;

using System.Collections.Generic;
using eye.analytics.irmaxtemp.Models;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Dapper;
using DocumentFormat.OpenXml.Office.CustomUI;


namespace eye.analytics.irmaxtemp.DataAccess
{
    public class AnalyticDataAccess : IAnalyticDataAccess
    {   

        private ILogger<AnalyticDataAccess> _logger;

        private string _connectionString;

        public AnalyticDataAccess(ILogger<AnalyticDataAccess> logger, IConfiguration configuration)
        {
            this._logger = logger;
            _connectionString = configuration.GetConnectionString("PostgreConnection");
        }

        private const string GET_IR_CONFIGURATIONS = @"select 
itc.asset_id as AssetId,
itc.asset_detail_id as AssetDetailId,
hs.value_double as LatestValue
from (
public.ir_thermography_configuration as itc 
inner join public.history_snapshot as hs on itc.asset_detail_id=hs.asset_detail_id
left join public.asset_details as ad on itc.asset_detail_id=ad.id 
left join public.device_details as dd on itc.device_detail_id=dd.id) 
where itc.status_id=1 
and ad.status_id=1
and dd.status_id=1
group by itc.asset_id, itc.asset_detail_id, hs.value_double;";


        private const string GET_IR_OUTPUT_TAGS = @"select
ad.asset_id as AssetId,
ad.id as AssetDetailId,
hs.value_double as LatestValue
from 
public.asset_details as ad
left join
public.history_snapshot as hs
on ad.id = hs.asset_detail_id
where
ad.asset_id in (select distinct asset_id from public.ir_thermography_configuration)
and ad.tag_name = 'IRMAXTEMP'
and ad.status_id = 1;";


        public async Task<List<ConfiguredIrAnalytic>> GetAnalyticsConfigured()
        {
            List<ConfiguredIrAnalytic> result = new List<ConfiguredIrAnalytic>();
            try
            {
                using (NpgsqlConnection connection = new NpgsqlConnection(_connectionString))
                {
                    await connection.OpenAsync();   
                    var config_list = await connection.QueryAsync<ConfiguredAnalyticDB>(GET_IR_CONFIGURATIONS);

                    var output_tag_list = await connection.QueryAsync<AnalyticOutputDB>(GET_IR_OUTPUT_TAGS);

                    if (config_list != null && output_tag_list!=null) {
                        Dictionary<int, List<AnalyticInput>> input_dict = new Dictionary<int, List<AnalyticInput>>();
                        Dictionary<int, AnalyticInput> output_dict = new Dictionary<int, AnalyticInput>();  

                        foreach (var item in config_list) {
                            if (!input_dict.ContainsKey(item.AssetId))
                            {
                                input_dict[item.AssetId] = new List<AnalyticInput>(); 
                            }
                            var input = new AnalyticInput(item.AssetDetailId, item.LatestValue);
                            input_dict[item.AssetId].Add(input);
                        }

                        foreach(var item in output_tag_list)
                        {
                            if (!output_dict.ContainsKey(item.AssetId))
                            {
                                output_dict[item.AssetId] = new AnalyticInput();
                            }
                            output_dict[item.AssetId].asset_detail_id = item.AssetDetailId;
                            output_dict[item.AssetId].latest_value = item.LatestValue;
                        }

                        foreach(var item in input_dict)
                        {
                            if (output_dict.ContainsKey(item.Key))
                            {
                                ConfiguredIrAnalytic analytic = new ConfiguredIrAnalytic();
                                analytic.asset_id = item.Key;
                                analytic.analyticInputs = item.Value;
                                analytic.analyticOutput = new AnalyticOutput() { asset_id = item.Key, output_tag_id = output_dict[item.Key].asset_detail_id, max_temp= output_dict[item.Key].latest_value }; 
                                result.Add(analytic);
                            }
                            
                        }
                        
                    }
                    return result;
                }
            }
            catch (Exception ex) {
                _logger.LogError("Exception Occureed at: {time} Exception : {exception}", DateTimeOffset.Now, ex);
                return result;
            }
        }
    }
}
