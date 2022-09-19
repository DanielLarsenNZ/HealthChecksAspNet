using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using static HealthChecksCommon.Constants;

namespace HealthChecksCommon
{
    public static class HealthChecksServiceCollectionExtensions
    {
        public static IServiceCollection AddHealthChecksDotNet(
            this IServiceCollection services, IConfiguration config)
        {
            //TODO: Configurable timeout/s
            var timeout = TimeSpan.FromSeconds(DefaultTimeoutSeconds);

            services.AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy())
                .AddApplicationInsightsPublisher(instrumentationKey: config["APPINSIGHTS_INSTRUMENTATIONKEY"], saveDetailedReport: true, excludeHealthyReports: true);

            // REDIS
            if (!string.IsNullOrWhiteSpace(config[RedisConnectionString]))
            {
                try
                {
                    services.AddHealthChecks()
                        .AddRedis(config[RedisConnectionString], name: "Redis", tags: new[] { "services", "redis" }, timeout: timeout);
                }
                catch (Exception ex)
                {
                    services.AddHealthChecks().AddCheck("Redis", () => HealthCheckResult.Unhealthy(exception: ex));
                }
            }

            // AZURE SERVICE BUS
            if (!string.IsNullOrWhiteSpace(config[AzureServiceBusConnectionString]) && !string.IsNullOrWhiteSpace(config[AzureServiceBusQueueName]))
            {
                try
                {
                    services.AddHealthChecks()
                        .AddAzureServiceBusQueue(
                            config[AzureServiceBusConnectionString],
                            config[AzureServiceBusQueueName],
                            new DefaultAzureCredential(),
                            name: "Azure Service Bus",
                            timeout: timeout,
                            tags: new[] { "services", "azure-service-bus" });
                }
                catch (Exception ex)
                {
                    services.AddHealthChecks().AddCheck("Azure Service Bus", () => HealthCheckResult.Unhealthy(exception: ex));
                }
            }

            // AZURE COSMOS DB
            if (!string.IsNullOrWhiteSpace(config[AzureCosmosDbEndpointUri]) && !string.IsNullOrWhiteSpace(config[AzureCosmosDbDatabaseName]))
            {
                try
                {
                    if (Uri.IsWellFormedUriString(config[AzureCosmosDbEndpointUri], UriKind.Absolute))
                    {
                        services.AddHealthChecks()
                        .AddCosmosDb(
                        config[AzureCosmosDbEndpointUri],
                        new DefaultAzureCredential(),
                        name: "Azure Cosmos DB",
                        database: config[AzureCosmosDbDatabaseName],
                        timeout: timeout,
                        tags: new[] { "services", "azure-cosmosdb" });
                    }
                    //TODO: Log malformed URI
                }
                catch (Exception ex)
                {
                    services.AddHealthChecks().AddCheck("Azure Cosmos DB", () => HealthCheckResult.Unhealthy(exception: ex));
                }
            }

            // AZURE KEY VAULT
            if (!string.IsNullOrWhiteSpace(config[AzureKeyVaultUri]))
            {
                try
                {
                    if (Uri.IsWellFormedUriString(config[AzureKeyVaultUri], UriKind.Absolute))
                    {
                        services.AddHealthChecks()
                            .AddAzureKeyVault(
                            new Uri(config[AzureKeyVaultUri]),
                            new DefaultAzureCredential(),
                            (options) => { },
                            name: "Azure Key Vault",
                            timeout: timeout);
                    }
                }
                catch (Exception ex)
                {
                    services.AddHealthChecks().AddCheck("Azure Key Vault", () => HealthCheckResult.Unhealthy(exception: ex));
                }
            }

            // AZURE STORAGE
            if (!string.IsNullOrWhiteSpace(config[AzureStorageBlobEndpointUri]))
            {
                try
                {
                    if (Uri.IsWellFormedUriString(config[AzureStorageBlobEndpointUri], UriKind.Absolute))
                    {
                        services.AddSingleton((services) => new BlobServiceClient(new Uri(config[AzureStorageBlobEndpointUri]), new DefaultAzureCredential()));
                        services.AddHealthChecks()
                            .AddAzureBlobStorage(
                            (options) => options.ContainerName = config[AzureStorageContainerName],
                            name: "Azure Blob Storage",
                            timeout: timeout);
                    }
                    //TODO: Log malformed URI string
                }
                catch (Exception ex)
                {
                    services.AddHealthChecks().AddCheck("Azure Blob Storage", () => HealthCheckResult.Unhealthy(exception: ex));
                }
            }

            // SQL SERVER
            if (!string.IsNullOrWhiteSpace(config[SqlServerConnectionString]))
            {
                try
                {
                    services.AddHealthChecks()
                        .AddSqlServer(
                        config[SqlServerConnectionString],
                        name: "SQL Server",
                        timeout: timeout);
                }
                catch (Exception ex)
                {
                    services.AddHealthChecks().AddCheck("SQL Server", () => HealthCheckResult.Unhealthy(exception: ex));
                }
            }

            // HTTPS ENDPOINTS
            if (!string.IsNullOrEmpty(config[HttpsEndpointUrls]))
            {
                string urls = config[HttpsEndpointUrls];
                foreach (string url in urls.Split(';'))
                {
                    if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
                    {
                        services.AddHealthChecks().AddCheck(url, () => HealthCheckResult.Unhealthy($"{url} is not a well-formed absolute URI string. Check app setting {HttpsEndpointUrls} and try again."));
                        continue;
                    }

                    var uri = new Uri(url);
                    services.AddHealthChecks().AddAsyncCheck(uri.ToString(), async () =>
                    {
                        var data = new Dictionary<string, object>(
                                    new KeyValuePair<string, object>[]
                                    {
                            new KeyValuePair<string, object>("IP", GetHostIps(uri))
                                    });

                        using (var http = new HttpClient())
                        {
                            try
                            {
                                http.Timeout = TimeSpan.FromSeconds(10);
                                http.DefaultRequestHeaders.ConnectionClose = true;
                                var result = await http.GetAsync(uri);
                                result.EnsureSuccessStatusCode();
                                return HealthCheckResult.Healthy(data: data);
                            }
                            catch (Exception ex)
                            {
                                return HealthCheckResult.Unhealthy(exception: ex, data: data);
                            }
                        }
                    },
                    tags: new[] { "endpoints" }, timeout: timeout);
                }
            }
            return services;
        }

        private static string GetHostIps(Uri uri)
        {
            try
            {
                var ips = System.Net.Dns.GetHostAddresses(uri.Host);
                return $"{string.Join(',', ips.Select(ip => ip.ToString()).ToArray())}";
            }
            catch (Exception ex)
            {
                //TODO: log and continue
                return ex.Message;
            }
        }
    }
}
