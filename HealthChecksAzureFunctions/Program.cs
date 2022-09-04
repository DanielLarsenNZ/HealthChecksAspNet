using HealthChecksCommon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;

internal class Program
{
    static async Task Main(string[] args)
    {
        var host = new HostBuilder()
                .ConfigureAppConfiguration(configurationBuilder =>
                {
                    configurationBuilder.AddEnvironmentVariables();
                })
                .ConfigureFunctionsWorkerDefaults()
                .ConfigureServices((builder, services) =>
                {
                    var config = builder.Configuration;
                    services.AddHealthChecksDotNet(config);
                })
                .Build();

        await host.RunAsync();
    }
}