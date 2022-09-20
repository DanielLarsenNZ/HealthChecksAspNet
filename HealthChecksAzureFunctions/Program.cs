using Azure.Identity;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading.Tasks;
using static HealthChecksCommon.Constants;

internal class Program
{
    static async Task Main(string[] args)
    {
        var host = new HostBuilder()
            .ConfigureAppConfiguration(configurationBuilder =>
            {
                configurationBuilder.AddEnvironmentVariables();
                //configurationBuilder.AddJsonFile("local.settings.json", true);
            })
            .ConfigureFunctionsWorkerDefaults()
            .ConfigureServices((builder, services) =>
            {
                var config = builder.Configuration;

                services.AddAzureClients(builder =>
                    {
                        builder.UseCredential(new DefaultAzureCredential());

                        // AZURE SERVICE BUS - Connection String
                        if (!string.IsNullOrWhiteSpace(config[AzureServiceBusConnectionString]))
                        {
                            builder.AddServiceBusAdministrationClient(config[AzureServiceBusConnectionString]);
                        }

                        // AZURE STORAGE
                        // Endpoint URI
                        if (!string.IsNullOrWhiteSpace(config[AzureStorageBlobEndpointUri]))
                        {
                            if (Uri.IsWellFormedUriString(config[AzureStorageBlobEndpointUri], UriKind.Absolute))
                            {
                                builder.AddBlobServiceClient(new Uri(config[AzureStorageBlobEndpointUri]));
                            }
                            else
                            {
                                //TODO: Log malformed URI
                            }
                        }
                        // Or Connection String
                        else if (!string.IsNullOrWhiteSpace(config[AzureStorageBlobConnectionString]))
                        {
                            builder.AddBlobServiceClient(config[AzureStorageBlobConnectionString]);
                        }

                        // AZURE KEY VAULT
                        if (!string.IsNullOrWhiteSpace(config[AzureKeyVaultUri]))
                        {
                            if (Uri.IsWellFormedUriString(config[AzureKeyVaultUri], UriKind.Absolute))
                            {
                                builder.AddSecretClient(new Uri(config[AzureKeyVaultUri]));
                            }
                            else
                            {
                                //TODO: Log malformed URI
                            }
                        }
                    });

                // AZURE SERVICE BUS - Endpoint
                if (!string.IsNullOrWhiteSpace(config[AzureServiceBusFQNamespace]))
                {
                    services.AddSingleton(new ServiceBusAdministrationClient(config[AzureServiceBusFQNamespace], new DefaultAzureCredential()));
                }

                // SQL SERVER
                if (!string.IsNullOrWhiteSpace(config[SqlServerConnectionString]))
                {
                    services.AddSingleton(new SqlConnection(config[SqlServerConnectionString]));
                }
            })
            .Build();

        await host.RunAsync();
    }
}