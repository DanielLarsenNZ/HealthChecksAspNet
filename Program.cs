using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text;

const string RedisConnectionString = "REDIS_CONNECTION_STRING";
const string AzureServiceBusConnectionString = "AZURE_SERVICE_BUS_CONNECTION_STRING";
const string AzureServiceBusQueueName = "AZURE_SERVICE_BUS_QUEUE_NAME";
const string AzureCosmosDbConnectionString = "AZURE_COSMOSDB_CONNECTION_STRING";
const string AzureCosmosDbDatabaseName = "AZURE_COSMOSDB_DATABASE_NAME";
const string HttpsEndpointUrls = "HTTPS_ENDPOINT_URLS";

var http = new HttpClient();

var builder = WebApplication.CreateBuilder(args);

var config = builder.Configuration;

// Add services to the container.
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy())
    .AddApplicationInsightsPublisher(instrumentationKey: config["APPINSIGHTS_INSTRUMENTATIONKEY"], saveDetailedReport: true, excludeHealthyReports: true);

if (!string.IsNullOrWhiteSpace(config[RedisConnectionString]))
{
    builder.Services.AddHealthChecks()
        .AddRedis(config[RedisConnectionString], tags: new[] { "services", "redis" }, timeout: TimeSpan.FromSeconds(10));
}

if (!string.IsNullOrWhiteSpace(config[AzureServiceBusConnectionString]))
{
    builder.Services.AddHealthChecks()
        .AddAzureServiceBusQueue(config[AzureServiceBusConnectionString], config[AzureServiceBusQueueName], timeout: TimeSpan.FromSeconds(10), tags: new[] { "services", "azure-service-bus" });
}

if (!string.IsNullOrWhiteSpace(config[AzureCosmosDbConnectionString]))
{
    builder.Services.AddHealthChecks()
        .AddCosmosDb(config[AzureCosmosDbConnectionString], database: config[AzureCosmosDbDatabaseName], timeout: TimeSpan.FromSeconds(10), tags: new[] { "services", "azure-cosmosdb" });
}

if (!string.IsNullOrEmpty(config[HttpsEndpointUrls]))
{
    string urls = config[HttpsEndpointUrls];
    foreach (string url in urls.Split(';'))
    {
        builder.Services.AddHealthChecks().AddAsyncCheck(url, async () =>
        {
            await http.GetAsync(url);
            return HealthCheckResult.Healthy();
        },
        tags: new[] { "endpoints" }, timeout: TimeSpan.FromSeconds(10));
    }
}

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = WriteResponse
});

app.UseHealthChecks("/", new HealthCheckOptions
{
    Predicate = r => r.Name.Contains("self"),
    ResponseWriter = WriteResponse
});

app.MapGet("/hello", () =>
{
    return "hello";
});

app.Run();


static Task WriteResponse(HttpContext context, HealthReport healthReport)
{
    context.Response.ContentType = "text/plain; charset=utf-8";

    using var memoryStream = new MemoryStream();
    using (var writer = new StreamWriter(memoryStream))
    {
        writer.WriteLine(healthReport.Status.ToString());

        foreach (var entry in healthReport.Entries)
        {
            writer.Write(entry.Key + '\t');
            writer.Write(entry.Value.Status.ToString() + '\t');
            writer.Write(entry.Value.Description + '\n');

            foreach (var item in entry.Value.Data)
            {
                writer.WriteLine($"\t{item.Key}: {item.Value}");
            }
        }
    }

    return context.Response.WriteAsync(Encoding.UTF8.GetString(memoryStream.ToArray()));
}