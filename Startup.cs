using System;
using System.IO;
using eye.analytics.irmaxtemp.Calculation;
using eye.analytics.irmaxtemp.DataAccess;
using eye.analytics.irmaxtemp.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;


namespace eye.analytics.irmaxtemp
{
    public class Startup
    {
        private const string _connectionString = "PostgreConnection";
        readonly string CrosOrigins = "_eyeAllowSpecificOrigins";
        public Startup(IHostEnvironment env)
        {
            var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);
            if (!env.IsDevelopment())
            {
                builder.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "..", "global.appsettings.json"), reloadOnChange: true, optional: true);
            }
            builder.AddEnvironmentVariables();
            Configuration = builder.Build();
        }
        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            string[] CrosOriginUrls = Configuration.GetSection("CrosOrigins")?.Value?.Split(';');
            if (CrosOriginUrls.Length > 0)
            {
                services.AddCors(options =>
                {
                    options.AddPolicy(name: CrosOrigins
                        , builder => builder
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials()
                        .WithOrigins(CrosOriginUrls)
                        );
                });
            }

            services.Configure<KestrelServerOptions>(Configuration.GetSection("Kestrel"));
            services.Configure<CommunicatorMQSettings>(Configuration.GetSection("MessageQueue"));
            services.AddTransient<IAnalyticDataAccess, AnalyticDataAccess>();
            services.AddTransient<IAnalyticCalculation, AnalyticCalculation>();
            services.AddHostedService<Worker>();
            services.AddLogging(logBuilder =>
            {
                logBuilder.AddSerilog();
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseCors(CrosOrigins);
            app.UseRouting();
        }
    }
}
