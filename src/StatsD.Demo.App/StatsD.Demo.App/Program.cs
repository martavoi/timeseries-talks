using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using JustEat.StatsD;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace StatsD.Demo.App
{
    class Program
    {
        private const string ServiceName = "influxdb-statsd-demo";

        public static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Verbose)
                .Enrich.WithProperty("app", ServiceName)
                .WriteTo.Console()
                .CreateLogger();

            var services = new ServiceCollection();
            services.AddStatsD(serviceProvider =>
            {
                var statsdLogger = serviceProvider.GetRequiredService<ILogger<Program>>();
                return new StatsDConfiguration
                {
                    Host = IPAddress.Loopback.ToString(),
                    SocketProtocol = SocketProtocol.Udp,
                    OnError = exception =>
                    {
                        statsdLogger.LogWarning(exception, "Error while trying to publish statsd data");
                        return true;
                    }
                };
            });

            services.AddLogging(builder => builder.AddSerilog());
            
            var containerBuilder = new ContainerBuilder();
            containerBuilder.Populate(services);
            var container = containerBuilder.Build();

            var statsPublisher = container.Resolve<IStatsDPublisher>();
            var log = container.Resolve<ILogger<Program>>();

            log.LogInformation("Starting...");
            var rand = new Random();
            foreach (var i in Enumerable.Range(0, 500000))
            {
                statsPublisher.Increment("work-incr");
                log.LogTrace("work-incr has been increased {incr} times", i);

                var gaugeVal = rand.NextDouble() * rand.Next(0, 500);
                statsPublisher.Gauge(gaugeVal, "work-gauge");
                log.LogTrace("work-gauge has been set to {gauge_value}", i);

                using (statsPublisher.StartTimer("work-1"))
                {
                    var millisecondsDelay = rand.Next(800);
                    await Task.Delay(millisecondsDelay).ConfigureAwait(false);
                    log.LogTrace("work-1 has completed in {work_1_ms}ms", millisecondsDelay);
                }

                using (statsPublisher.StartTimer("work-2"))
                {
                    var millisecondsDelay = rand.Next(300);
                    await Task.Delay(millisecondsDelay).ConfigureAwait(false);
                    log.LogTrace("work-2 has completed in {work_2_ms}ms", millisecondsDelay);
                }

                using (statsPublisher.StartTimer("work-3"))
                {
                    var millisecondsDelay = rand.Next(10);
                    await Task.Delay(millisecondsDelay).ConfigureAwait(false);
                    log.LogTrace("work-3 has completed in {work_3_ms}ms", millisecondsDelay);
                }
            }

            log.LogInformation("Work complete. Pres any key to close console...");
            Console.ReadKey();
        }
    }
}
