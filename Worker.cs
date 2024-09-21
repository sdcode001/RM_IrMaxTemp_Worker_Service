using eye.analytics.irmaxtemp.Calculation;
using eye.analytics.irmaxtemp.DataAccess;
using eye.analytics.irmaxtemp.Helpers;
using eye.analytics.irmaxtemp.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;



namespace eye.analytics.irmaxtemp
{
    public class Worker : BackgroundService
    {

        private readonly ILogger<Worker> _logger;   
        private IAnalyticDataAccess _analyticDataAccess;
        private readonly CommunicatorMQSettings _mqSettings;
        private readonly IAnalyticCalculation _analyticCalculation;
        private List<ConfiguredIrAnalytic> _configuredAnalytics;
        private bool _isRestartRequired = false;
        private ConcurrentQueue<AnalyticOutput> _analyticOutputQueue;


        public Worker(IAnalyticDataAccess analyticDataAccess, ILogger<Worker> logger, IOptions<CommunicatorMQSettings> mqSettings, IAnalyticCalculation analyticCalculation)
        {
           this._analyticDataAccess = analyticDataAccess;
           this. _logger = logger;
           this._mqSettings = mqSettings.Value;
           this._analyticCalculation = analyticCalculation;
           _analyticOutputQueue = new ConcurrentQueue<AnalyticOutput>();
        }


        private async Task<List<ConfiguredIrAnalytic>> GetConfiguredAnalytics()
        {
            return await _analyticDataAccess.GetAnalyticsConfigured();
        }



        private async Task StartProcess(CancellationTokenSource tokenSource)
        {
            try
            {
                Console.WriteLine("Starting IrMaxTemp Service....");
                var stoppingToken = tokenSource.Token;

                Parallel.Invoke(new ParallelOptions{CancellationToken = stoppingToken},
                    async () => { 
                        Redis redisRef = new(_mqSettings.irMaxTempAnalyticQ.Server, Convert.ToInt32(_mqSettings.irMaxTempAnalyticQ.Port));
                        while (!stoppingToken.IsCancellationRequested)
                        {
                            try
                            {
                                var values = redisRef.LeftPop(_mqSettings.irMaxTempAnalyticQ.ListName, 100);
                                if (values != null) 
                                {
                                    for (int i = 0; i < values.Count(); i++)
                                    {
                                        var item = values[i];
                                        var msg = JsonSerializer.Deserialize<List<EyeMessageAnalytic>>(item);
                                        foreach (var data in msg) {
                                            switch (data.TagType)
                                            {
                                                case "1":
                                                    {
                                                        try
                                                        {
                                                            
                                                            foreach(var analytics in _configuredAnalytics) {
                                                                var assetId = analytics.asset_id;
                                                                var ourInput = false;
                                                                foreach (var input in analytics.analyticInputs) {
                                                                    if(data.TagId == input.asset_detail_id.ToString())
                                                                    {
                                                                        input.latest_value = double.Parse(data.Value);
                                                                        ourInput = true;
                                                                    }
                                                                }

                                                                // only calculate output if analytics input is updated(indicated using outInput) and input list size > 0
                                                                if (ourInput && analytics.analyticInputs.Count() > 0) {
                                                                    var calculatedOutput = _analyticCalculation.Calculate(analytics.analyticInputs);
                                                                    
                                                                    // only Enqueue into _analyticOutputQueue if currently calculated output != last calculated output
                                                                    if (!double.IsNaN(calculatedOutput) && (calculatedOutput!=analytics.analyticOutput.max_temp || analytics.analyticOutput.max_temp==null)) {     
                                                                        analytics.analyticOutput.max_temp = calculatedOutput;   
                                                                        _analyticOutputQueue.Enqueue(analytics.analyticOutput); 
                                                                    }
                                                                    
                                                                }                                                               
                                                            }
                                                        }
                                                        catch (Exception ex) {
                                                            _logger.LogError("Exception Occureed at: {time} Exception : {exception}", DateTimeOffset.Now, ex);
                                                        }

                                                        break;
                                                    }
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    if (!stoppingToken.IsCancellationRequested)
                                    {
                                        try
                                        {
                                            await Task.Delay(500, stoppingToken);
                                        }
                                        catch (Exception ex) {
                                            _logger.LogError("Exception Occureed at: {time} Exception : {exception}", DateTimeOffset.Now, ex);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError("Exception Occureed at: {time} Exception : {exception}", DateTimeOffset.Now, ex);
                            }
                        }
                    },

                    async () =>
                    {
                        while (!stoppingToken.IsCancellationRequested) {

                            if (_analyticOutputQueue.Count() > 0) {
                                AnalyticOutput analyticOutput;
                                while (_analyticOutputQueue.TryDequeue(out analyticOutput)) {
                                    if (analyticOutput != null)
                                    {
                                        try
                                        {
                                            List<AnalyticOutputToQueue> analyticsOutputList = new List<AnalyticOutputToQueue>();
                                            AnalyticOutputToQueue analyticsOutputToQueue = new();
                                            analyticsOutputToQueue.TagType = "50";
                                            analyticsOutputToQueue.TimeStamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                                            analyticsOutputToQueue.TagId = analyticOutput.output_tag_id.ToString();
                                            analyticsOutputToQueue.Value = analyticOutput.max_temp.ToString();

                                            analyticsOutputList.Add(analyticsOutputToQueue);

                                            string output = JsonSerializer.Serialize(analyticsOutputList);

                                            Redis redisRef = new(_mqSettings.uiq.Server, Convert.ToInt32(_mqSettings.uiq.Port));
                                            Console.WriteLine(string.Format("Outputing max_temp to output queues for TagId: {0}, Value: {1}", analyticOutput.output_tag_id.ToString(), analyticOutput.max_temp.ToString()));

                                            //redisRef.RightPush("uidataq", output);
                                            redisRef.RightPush(_mqSettings.uiq.ListName, output);
                                            redisRef.RightPush(_mqSettings.dblogq.ListName, output);
                                            redisRef.RightPush(_mqSettings.alarmq.ListName, output);
                                            redisRef.RightPush(_mqSettings.calcq.ListName, output);
                                            redisRef.RightPush(_mqSettings.commandq.ListName, output);
                                            redisRef.RightPush(_mqSettings.snapshotq.ListName, output);

                                            // redisRef.Dispose();
                                            redisRef = null;
                                        }
                                        catch (Exception ex) {
                                            _logger.LogError("Exception Occureed at: {time} Exception : {exception}", DateTimeOffset.Now, ex);
                                        }
                                    }
                                }
                            }
                            else{
                                if (!stoppingToken.IsCancellationRequested) {
                                    try
                                    {
                                        await Task.Delay(500, stoppingToken);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError("Exception Occureed at: {time} Exception : {exception}", DateTimeOffset.Now, ex);
                                    }
                                }                                    
                            }

                        }
                    },

                    async () =>
                    {
                        try
                        {
                            // check in irMaxTemp queue for restart the service
                            // irMaxTemp queue will be handeled by API, when new config insert into DB then API will also push data into irMaxTemp for restart
                            using Redis redisRef = new(_mqSettings.irMaxTempAnalyticQ.Server, Convert.ToInt32(_mqSettings.irMaxTempAnalyticQ.Port));
                            while (!stoppingToken.IsCancellationRequested) {
                                var values = redisRef.LeftPop(_mqSettings.irMaxTempQ.ListName, 1);
                                if (values != null) {
                                    if(values.Length == 1)
                                    {
                                        var item = values[0];
                                        var msg = JsonSerializer.Deserialize<EyeMessageAnalytic>(item);

                                        switch (msg.TagType) {
                                            case "11":
                                                {
                                                    _isRestartRequired = true;
                                                    break;
                                                }
                                            default:
                                                break;
                                        }
                                    }
                                    else
                                    {
                                        for(var i = 0; i<values.Length; i++)
                                        {
                                            var item = values[i];
                                            var msg = JsonSerializer.Deserialize<List<EyeMessageAnalytic>>(item);

                                            foreach(var data in msg)
                                            {
                                                switch (data.TagType)
                                                {
                                                    case "11":
                                                        {
                                                            _isRestartRequired = true;
                                                            break;
                                                        }
                                                    default:
                                                        break;
                                                }
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    if (!stoppingToken.IsCancellationRequested)
                                    {
                                        try
                                        {
                                            await Task.Delay(500, stoppingToken);
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogError("Exception Occureed at: {time} Exception : {exception}", DateTimeOffset.Now, ex);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex) {
                            _logger.LogError("Exception Occureed at: {time} Exception : {exception}", DateTimeOffset.Now, ex);
                        }
                    },

                    async () =>
                    {
                        // trigger the restart flag.
                        try
                        {
                            while (!stoppingToken.IsCancellationRequested)
                            {
                                if (_isRestartRequired == true)
                                {
                                    _isRestartRequired = false;
                                    tokenSource.Cancel(false);
                                }
                                if (!stoppingToken.IsCancellationRequested)
                                {
                                    await Task.Delay(1000 * 5);
                                }                                
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError("Exception Occureed at: {time} Exception : {exception}", DateTimeOffset.Now, ex);
                        }
                    }
                );

            }
            catch(Exception ex) {
                _logger.LogError("Exception Occureed at: {time} Exception : {exception}", DateTimeOffset.Now, ex);
            }
        }



        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                CancellationTokenSource tokenSource2 = null;
                try
                {
                    do
                    {
                        if (tokenSource2 == null || tokenSource2.IsCancellationRequested)
                        {
                            tokenSource2 = new();

                            _configuredAnalytics = await GetConfiguredAnalytics();    
                            await StartProcess(tokenSource2);
                        }
                        try
                        {
                            await Task.Delay(5 * 1000);
                        }
                        catch (Exception ex)
                        {

                        }
                    }
                    while (!stoppingToken.IsCancellationRequested);
                }
                catch (System.Exception ex)
                {
                    _logger.LogError("Exception Occureed at: {time} Exception : {exception}", DateTimeOffset.Now, ex);
                }

            }
        }
    }
}