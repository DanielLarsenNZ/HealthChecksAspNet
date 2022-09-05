using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
                services.AddHealthChecks()
                    .AddRedis(config[RedisConnectionString], tags: new[] { "services", "redis" }, timeout: timeout);
            }

            // AZURE SERVICE BUS
            if (!string.IsNullOrWhiteSpace(config[AzureServiceBusConnectionString]) && !string.IsNullOrWhiteSpace(config[AzureServiceBusQueueName]))
            {
                services.AddHealthChecks()
                    .AddAzureServiceBusQueue(
                    config[AzureServiceBusConnectionString],
                    config[AzureServiceBusQueueName],
                    timeout: timeout,
                    tags: new[] { "services", "azure-service-bus" });
            }

            // AZURE COSMOS DB
            if (!string.IsNullOrWhiteSpace(config[AzureCosmosDbConnectionString]) && !string.IsNullOrWhiteSpace(config[AzureCosmosDbDatabaseName]))
            {
                services.AddHealthChecks()
                    .AddCosmosDb(
                    config[AzureCosmosDbConnectionString],
                    database: config[AzureCosmosDbDatabaseName],
                    timeout: timeout,
                    tags: new[] { "services", "azure-cosmosdb" });
            }

            // AZURE KEY VAULT
            if (!string.IsNullOrWhiteSpace(config[AzureKeyVaultUri]))
            {
                if (Uri.IsWellFormedUriString(config[AzureKeyVaultUri], UriKind.Absolute))
                {
                    services.AddHealthChecks()
                        .AddAzureKeyVault(new Uri(config[AzureKeyVaultUri]), new DefaultAzureCredential(), (options) => { }, timeout: timeout);
                }
                //TODO: log malformed URI string
            }

            // AZURE STORAGE
            if (!string.IsNullOrWhiteSpace(config[AzureStorageConnectionString]) && !string.IsNullOrWhiteSpace(config[AzureStorageContainerName]))
            {
                services.AddSingleton((services)
                    => new BlobServiceClient(config[AzureStorageConnectionString]));

                services.AddHealthChecks().AddAzureBlobStorage((options) => options.ContainerName = "data", timeout: timeout);

            }

            // SQL SERVER
            if (!string.IsNullOrWhiteSpace(config[SqlServerConnectionString]))
            {
                services.AddHealthChecks()
                    .AddSqlServer(config[SqlServerConnectionString], timeout: timeout);
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
                            try
                            {
                                await http.GetAsync(uri);
                                return HealthCheckResult.Healthy(data: data);
                            }
                            catch (Exception ex)
                            {
                                return HealthCheckResult.Unhealthy(exception: ex, data: data);
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
