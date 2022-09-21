using Azure.Messaging.ServiceBus.Administration;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using static HealthChecksCommon.Constants;

namespace HealthChecksCommon
{
    public class HealthChecksService
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _config;
        private readonly IServiceProvider _services;

        public HealthChecksService(
            ILoggerFactory loggerFactory,
            IConfiguration config,
            IServiceProvider services)
        {
            _logger = loggerFactory.CreateLogger<HealthChecksService>();
            _config = config;
            _services = services;
        }

        public async Task<HealthReport> RunHealthChecks()
        {
            var status = HealthStatus.Healthy;
            var healthReportEntries = new ConcurrentDictionary<string, HealthReportEntry>();
            var allChecksStartTime = DateTimeOffset.Now;

            try
            {
                //var tasks = new List<Task>();
                await HealthCheck<BlobServiceClient>(healthReportEntries, (service, cancellationToken) => HealthCheckBlobStorage(service, cancellationToken));

                
                await HealthCheck<ServiceBusAdministrationClient>(healthReportEntries, async (service, cancellationToken) =>
                {
                    var startTime = DateTimeOffset.Now;

                    if (string.IsNullOrEmpty(_config[AzureServiceBusQueueName]))
                    {
                        // No queue to check. Just get all queues
                        var queues = service.GetQueuesAsync(cancellationToken);

                        await foreach (var queue in queues)
                        {
                            _logger.LogTrace($"First queue found {queue.Name}");
                            break;
                        }

                        return Healthy($"Get Queues succeeded. No queue name specified by app setting \"{AzureServiceBusQueueName}\".", startTime);
                    }
                    else
                    {
                        var queue = await service.GetQueueAsync(_config[AzureServiceBusQueueName], cancellationToken);

                        if (queue.Value is null)
                        {
                            return Unhealthy($"Queue \"{_config[AzureServiceBusQueueName]}\" not found.", startTime);
                        }
                        else
                        {
                            return Healthy($"Queue \"{_config[AzureServiceBusQueueName]}\" exists.", startTime);
                        }
                    }
                });

                await HealthCheck<SecretClient>(healthReportEntries, async (service, cancellationToken) =>
                {
                    var startTime = DateTimeOffset.Now;

                    var properties = service.GetPropertiesOfSecretsAsync(cancellationToken);

                    await foreach (var property in properties)
                    {
                        _logger.LogTrace($"First property found {property.Name}");
                        break;
                    }

                    return Healthy("List secrets succeeded", startTime);
                });

                await HealthCheck<SqlConnection>(healthReportEntries, async (service, cancellationToken) =>
                {
                    var startTime = DateTimeOffset.Now;

                    try
                    {
                        await service.OpenAsync(cancellationToken);

                        string sql = "SELECT @@VERSION";

                        using (SqlCommand command = new SqlCommand(sql, service))
                        {
                            using (SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
                            {
                                while (reader.Read())
                                {
                                    _logger.LogTrace(reader.GetString(0));
                                    break;
                                }
                            }
                        }
                    }
                    finally
                    {
                        await service.CloseAsync();
                    }

                    return Healthy("SELECT @@VERSION succeeded", startTime);
                });
                

                //await Task.WhenAll(tasks);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
                status = HealthStatus.Unhealthy;
                healthReportEntries.TryAdd(nameof(HealthChecksService), new HealthReportEntry(HealthStatus.Unhealthy, ex.Message, Elapsed(allChecksStartTime), ex, null));
            }

            // If any health report entries are unhealthy, set Health report status to unhealthy
            if (healthReportEntries.Any(entry => entry.Value.Status == HealthStatus.Unhealthy)) status = HealthStatus.Unhealthy;

            return new HealthReport(healthReportEntries, status, Elapsed(allChecksStartTime));

        }

        public async Task<HealthReportEntry> HealthCheckBlobStorage(BlobServiceClient service, CancellationToken cancellationToken)
        {
            var startTime = DateTimeOffset.Now;

            // Call the listing operation and enumerate the result segment.
            var resultSegment = service.GetBlobContainersAsync(
                BlobContainerTraits.Metadata,
                prefix: _config[AzureStorageContainerName],
                cancellationToken: cancellationToken)
                .AsPages();

            await foreach (Azure.Page<BlobContainerItem> containerPage in resultSegment)
            {
                foreach (BlobContainerItem containerItem in containerPage.Values)
                {
                    if (string.IsNullOrEmpty(_config[AzureStorageContainerName]))
                    {
                        return Healthy($"List containers succeeded. No container name specified by app setting \"{AzureStorageContainerName}\".", startTime);
                    }

                    if (containerItem.Name == _config[AzureStorageContainerName])
                    {
                        return Healthy($"List containers succeeded. Container \"{_config[AzureStorageContainerName]}\" exists.", startTime);
                    }
                }
            }

            // Container not found
            return Unhealthy($"List containers succeeded, but container \"{_config[AzureStorageContainerName]}\" not found.", startTime);
        }

        private async Task HealthCheck<T>(ConcurrentDictionary<string, HealthReportEntry> healthReportEntries, Func<T, CancellationToken, Task<HealthReportEntry>> healthcheckLogic)
        {
            var service = _services.GetService<T>();

            // Don't run the health check if the service has not been configured.
            if (service == null)
            {
                _logger.LogInformation($"{typeof(T).FullName} not found in Service Provider.");
                return;
            }

            string key = typeof(T).Name;
            DateTimeOffset startTime = DateTimeOffset.Now;
            var tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(DefaultTimeoutMilliseconds);

            try
            {
                var healthReportEntry = await healthcheckLogic(service, tokenSource.Token);
                if (!healthReportEntries.TryAdd(key, healthReportEntry))
                    throw new InvalidOperationException();
            }
            catch (TaskCanceledException ex)
            {
                var timeoutException = new TimeoutException($"Task was cancelled after timeout of {DefaultTimeoutSeconds} seconds.", ex);
                AddUnhealthy(healthReportEntries, key, timeoutException, startTime);
                _logger.LogError(timeoutException, timeoutException.Message);
            }
            catch (Exception ex)
            {
                AddUnhealthy(healthReportEntries, key, ex, startTime);
                _logger.LogError(ex, ex.Message);
            }
        }

        private static TimeSpan Elapsed(DateTimeOffset startTime) => DateTimeOffset.Now.Subtract(startTime);

        private static HealthReportEntry Healthy(string description, DateTimeOffset startTime) =>
                new HealthReportEntry(
                    HealthStatus.Healthy,
                    description,
                    Elapsed(startTime),
                    null,
                    null);

        private static HealthReportEntry Unhealthy(string description, DateTimeOffset startTime) =>
        new HealthReportEntry(
            HealthStatus.Unhealthy,
            description,
            Elapsed(startTime),
            null,
            null);

        private static void AddHealthy(ConcurrentDictionary<string, HealthReportEntry> healthReportEntries, string key, string description, DateTimeOffset startTime)
        {
            if (!healthReportEntries.TryAdd(key, Healthy(description, startTime)))
            {
                throw new InvalidOperationException();
            }
        }

        private static void AddUnhealthy(ConcurrentDictionary<string, HealthReportEntry> healthReportEntries, string key, string description, DateTimeOffset startTime)
        {
            if (!healthReportEntries.TryAdd(key, Unhealthy(description, startTime)))
            {
                throw new InvalidOperationException();
            }
        }

        private static void AddUnhealthy(ConcurrentDictionary<string, HealthReportEntry> healthReportEntries, string key, Exception exception, DateTimeOffset startTime)
        {
            if (!healthReportEntries.TryAdd(
                key,
                new HealthReportEntry(
                    HealthStatus.Unhealthy,
                    exception.Message,
                    Elapsed(startTime),
                    exception,
                    null)))
            {
                throw new InvalidOperationException();
            }
        }

        public static HttpStatusCode HttpStatusFromHealthCheckStatus(HealthReport healthReport)
        {
            if (healthReport.Status == HealthStatus.Healthy) return HttpStatusCode.OK;
            return HttpStatusCode.ServiceUnavailable;
        }
    }
}
