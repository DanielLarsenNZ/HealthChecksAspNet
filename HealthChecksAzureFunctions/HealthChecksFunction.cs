using Azure.Messaging.ServiceBus.Administration;
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
    public class HealthChecksFunction
    {
        private readonly ILogger _logger;
        private readonly HealthChecksService _healthChecksService;

        public HealthChecksFunction(
            ILoggerFactory loggerFactory,
            HealthChecksService healthChecksService
            )
        {
            _logger = loggerFactory.CreateLogger<HealthChecksFunction>();
            _healthChecksService = healthChecksService;
        }

        [Function(nameof(HealthChecksFunction))]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var healthReport = await _healthChecksService.RunHealthChecks();

            var response = req.CreateResponse(HealthChecksService.HttpStatusFromHealthCheckStatus(healthReport));
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            await response.WriteStringAsync(HealthChecksDotNetResponseWriter.WriteResponseString(healthReport));

            return response;
        }
    }
}
