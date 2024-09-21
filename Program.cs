using eye.analytics.irmaxtemp.Calculation;
using eye.analytics.irmaxtemp.DataAccess;
using eye.analytics.irmaxtemp.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.IO;

namespace eye.analytics.irmaxtemp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            var builder = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                IHostEnvironment env = hostingContext.HostingEnvironment;
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                   .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);
                if (!env.IsDevelopment())
                {
                    config.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "..", "global.appsettings.json"), reloadOnChange: true, optional: true);
                }
                if (env.IsDevelopment() && !string.IsNullOrEmpty(env.ApplicationName))
                {
                   
                    System.Reflection.Assembly appAssembly =
                        System.Reflection.Assembly.Load(new System.Reflection.AssemblyName(env.ApplicationName));
                    if (appAssembly != null)
                    {
                        config.AddUserSecrets(appAssembly, optional: true);
                    }
                }
                config.AddEnvironmentVariables();

                if (args != null)
                {
                    config.AddCommandLine(args);
                }
            })
            .ConfigureServices((context, services) =>
            {
                services.Configure<CommunicatorMQSettings>(context.Configuration.GetSection("MessageQueue"));
                services.AddTransient<IAnalyticDataAccess, AnalyticDataAccess>();
                services.AddTransient<IAnalyticCalculation, AnalyticCalculation>();
                services.AddHostedService<Worker>();
                services.AddLogging(logBuilder =>
                {
                    logBuilder.AddSerilog();
                });
                services.AddHostedService<Worker>();
            });
            return builder;
        }
    }
}
