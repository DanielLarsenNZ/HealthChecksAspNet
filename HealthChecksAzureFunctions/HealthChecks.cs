using Azure.Messaging.ServiceBus;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using HealthChecksCommon;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
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

namespace HealthChecksAzureFunctions
{
    public class HealthChecks
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _config;
        private readonly IServiceProvider _services;

        public HealthChecks(
            ILoggerFactory loggerFactory,
            IConfiguration config,
            IServiceProvider services)
        {
            _logger = loggerFactory.CreateLogger<HealthChecks>();
            _config = config;
            _services = services;
        }

        [Function(nameof(HealthChecks))]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");
            var status = HealthStatus.Healthy;
            var healthReportEntries = new ConcurrentDictionary<string, HealthReportEntry>();
            var startTime = DateTimeOffset.Now;

            try
            {
                var tokenSource = new CancellationTokenSource();
                tokenSource.CancelAfter(DefaultTimeoutMilliseconds * 2);
                var cancellationToken = tokenSource.Token;

                var tasks = new List<Task>();
                tasks.Add(HealthCheckAzureBlobStorage(healthReportEntries));
                //tasks.Add(HealthCheckAzureServiceBus(_logger, _config, cancellationToken, healthReportEntries, _serviceBusClient));
                //tasks.Add(HealthCheckSqlDb(_logger, _config, cancellationToken, healthReportEntries, _sqlConnection));
                //tasks.Add(HealthCheckAzureKeyVault(_logger, _config, cancellationToken, healthReportEntries, _secretClient));

                bool allCompleted = Task.WaitAll(tasks.ToArray(), DefaultTimeoutMilliseconds, cancellationToken);

                if (!allCompleted) throw new TimeoutException($"Tasks did not completed within timeout of {DefaultTimeoutSeconds * 2} seconds.");

            }
            catch (Exception ex)
            {
                status = HealthStatus.Unhealthy;
                healthReportEntries.TryAdd(nameof(HealthChecks), new HealthReportEntry(HealthStatus.Unhealthy, ex.Message, Elapsed(startTime), ex, null));
            }

            // If any health report entries are unhealthy, set Health report status to unhealthy
            if (healthReportEntries.Any(entry => entry.Value.Status == HealthStatus.Unhealthy)) status = HealthStatus.Unhealthy;

            var healthReport = new HealthReport(healthReportEntries, status, Elapsed(startTime));

            var response = req.CreateResponse(HttpStatusFromHealthCheckStatus(healthReport));
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            await response.WriteStringAsync(HealthChecksDotNetResponseWriter.WriteResponseString(healthReport));

            return response;
        }

        private Task HealthCheckAzureKeyVault(ILogger logger, IConfiguration config, CancellationToken cancellationToken, ConcurrentDictionary<string, HealthReportEntry> healthReportEntries, SecretClient secretClient)
        {
            throw new NotImplementedException();
        }

        private Task HealthCheckSqlDb(ILogger logger, IConfiguration config, CancellationToken cancellationToken, ConcurrentDictionary<string, HealthReportEntry> healthReportEntries, SqlConnection sqlConnection)
        {
            throw new NotImplementedException();
        }

        private Task HealthCheckAzureServiceBus(ILogger logger, IConfiguration config, CancellationToken cancellationToken, ConcurrentDictionary<string, HealthReportEntry> healthReportEntries, ServiceBusClient serviceBusClient)
        {
            throw new NotImplementedException();
        }

        private async Task HealthCheckAzureBlobStorage(ConcurrentDictionary<string, HealthReportEntry> healthReportEntries)
        {
            var blobServiceClient = _services.GetService<BlobServiceClient>();

            // Don't run the health check if the service has not been configured.
            if (blobServiceClient == null) return;

            const string key = "Azure Blob Storage";
            DateTimeOffset startTime = DateTimeOffset.Now;

            try
            {
                var tokenSource = new CancellationTokenSource();
                tokenSource.CancelAfter(DefaultTimeoutMilliseconds);

                // Call the listing operation and enumerate the result segment.
                var resultSegment = blobServiceClient.GetBlobContainersAsync(
                    BlobContainerTraits.Metadata,
                    prefix: _config[AzureStorageContainerName],
                    cancellationToken: tokenSource.Token)
                    .AsPages();

                await foreach (Azure.Page<BlobContainerItem> containerPage in resultSegment)
                {
                    foreach (BlobContainerItem containerItem in containerPage.Values)
                    {
                        if (string.IsNullOrEmpty(_config[AzureStorageContainerName]))
                        {
                            AddHealthy(healthReportEntries, key, $"List containers succeeded. No container name specified by app setting \"{AzureStorageContainerName}\".", startTime);
                            return;
                        }

                        if (containerItem.Name == _config[AzureStorageContainerName])
                        {
                            AddHealthy(healthReportEntries, key, $"List containers succeeded. Container \"{_config[AzureStorageContainerName]}\" exists.", startTime);
                            return;
                        }
                    }
                }

                // Container not found
                AddUnhealthy(healthReportEntries, key, $"List containers succeeded, but container \"{_config[AzureStorageContainerName]}\" not found.", startTime);
                return;
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

        private static void AddHealthy(ConcurrentDictionary<string, HealthReportEntry> healthReportEntries, string key, string description, DateTimeOffset startTime)
        {
            if (!healthReportEntries.TryAdd(
                key,
                new HealthReportEntry(
                    HealthStatus.Healthy,
                    description,
                    Elapsed(startTime),
                    null,
                    null)))
            {
                throw new InvalidOperationException();
            }
        }

        private static void AddUnhealthy(ConcurrentDictionary<string, HealthReportEntry> healthReportEntries, string key, string description, DateTimeOffset startTime)
        {
            if (!healthReportEntries.TryAdd(
                key,
                new HealthReportEntry(
                    HealthStatus.Unhealthy,
                    description,
                    Elapsed(startTime),
                    null,
                    null)))
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

        private static HttpStatusCode HttpStatusFromHealthCheckStatus(HealthReport healthReport)
        {
            if (healthReport.Status == HealthStatus.Healthy) return HttpStatusCode.OK;
            return HttpStatusCode.ServiceUnavailable;
        }
    }
}
