using HealthChecksCommon;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Threading.Tasks;

namespace HealthChecksAzureFunctions
{
    public class HealthChecks
    {
        private readonly ILogger _logger;
        private readonly HealthCheckService _healthChecks;

        public HealthChecks(ILoggerFactory loggerFactory, HealthCheckService healthChecks)
        {
            _logger = loggerFactory.CreateLogger<HealthChecks>();
            _healthChecks = healthChecks;
        }

        [Function(nameof(HealthChecks))]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var healthReport = await _healthChecks.CheckHealthAsync();
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            await response.WriteStringAsync(HealthChecksDotNetResponseWriter.WriteResponseString(healthReport));

            return response;
        }
    }
}
