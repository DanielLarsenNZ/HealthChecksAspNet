using Azure.Core;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Collections.ObjectModel;
using System.Text;

const string RedisConnectionString = "REDIS_CONNECTION_STRING";
const string AzureServiceBusConnectionString = "AZURE_SERVICE_BUS_CONNECTION_STRING";
const string AzureServiceBusQueueName = "AZURE_SERVICE_BUS_QUEUE_NAME";
const string AzureCosmosDbConnectionString = "AZURE_COSMOSDB_CONNECTION_STRING";
const string AzureCosmosDbDatabaseName = "AZURE_COSMOSDB_DATABASE_NAME";
const string HttpsEndpointUrls = "HTTPS_ENDPOINT_URLS";
const string EchoAllowedHosts = "ECHO_ALLOWED_HOSTS";

const int DefaultTimeoutSeconds = 2;

var builder = WebApplication.CreateBuilder(args);

var config = builder.Configuration;

// Add services to the container.
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy())
    .AddApplicationInsightsPublisher(instrumentationKey: config["APPINSIGHTS_INSTRUMENTATIONKEY"], saveDetailedReport: true, excludeHealthyReports: true);

if (!string.IsNullOrWhiteSpace(config[RedisConnectionString]))
{
    builder.Services.AddHealthChecks()
        .AddRedis(config[RedisConnectionString], tags: new[] { "services", "redis" }, timeout: TimeSpan.FromSeconds(DefaultTimeoutSeconds));
}

if (!string.IsNullOrWhiteSpace(config[AzureServiceBusConnectionString]))
{
    builder.Services.AddHealthChecks()
        .AddAzureServiceBusQueue(config[AzureServiceBusConnectionString], config[AzureServiceBusQueueName], timeout: TimeSpan.FromSeconds(DefaultTimeoutSeconds), tags: new[] { "services", "azure-service-bus" });
}

if (!string.IsNullOrWhiteSpace(config[AzureCosmosDbConnectionString]))
{
    builder.Services.AddHealthChecks()
        .AddCosmosDb(config[AzureCosmosDbConnectionString], database: config[AzureCosmosDbDatabaseName], timeout: TimeSpan.FromSeconds(DefaultTimeoutSeconds), tags: new[] { "services", "azure-cosmosdb" });
}

if (!string.IsNullOrEmpty(config[HttpsEndpointUrls]))
{
    string urls = config[HttpsEndpointUrls];
    foreach (string url in urls.Split(';'))
    {
        var uri = new Uri(url);
        builder.Services.AddHealthChecks().AddAsyncCheck(uri.ToString(), async () =>
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
        tags: new[] { "endpoints" }, timeout: TimeSpan.FromSeconds(DefaultTimeoutSeconds));
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

app.MapGet("/echo", async (string url) => 
{
    Uri uri;
    try
    {
        uri = new Uri(url);
    }
    catch (Exception)
    {
        return Results.BadRequest("Query parameter argument must be a well formed, fully qualified URL");
    }

    // If hostname is not contained in the Echo allowed hosts App setting value
    if (string.IsNullOrWhiteSpace(config[EchoAllowedHosts]) || !config[EchoAllowedHosts].Contains(uri.Host))
    {
        return Results.BadRequest($"Host {uri.Host} is not found in app setting {EchoAllowedHosts}.");
    }

    using (var http = new HttpClient())
    {
        var response = await http.GetAsync(uri);

        return Results.Content(
            $"RESPONSE STATUS {response.StatusCode}\n\n{await response.Content.ReadAsStringAsync()}", 
            contentType: "text/plain", 
            contentEncoding: Encoding.UTF8);
    }
});

app.Run();


static Task WriteResponse(HttpContext context, HealthReport healthReport)
{
    context.Response.ContentType = "text/plain; charset=utf-8";

    using var memoryStream = new MemoryStream();
    using (var writer = new StreamWriter(memoryStream))
    {
        writer.WriteLine(healthReport.Status.ToString().ToUpper());

        writer.WriteLine($"Request.Host:\t\t{context.Request.Host}");
        writer.WriteLine($"RemoteIpAddress:\t{context.Connection.RemoteIpAddress}");
        
        var httpConnectionFeature = context.Request.HttpContext.Features.Get<IHttpConnectionFeature>();
        writer.WriteLine($"LocalIpAddress:\t\t{httpConnectionFeature?.LocalIpAddress}");
        writer.WriteLine($"UtcNow:\t\t\t{DateTime.UtcNow}");
        writer.WriteLine($"WEBSITE_INSTANCE_ID:\t{Environment.GetEnvironmentVariables()["WEBSITE_INSTANCE_ID"]}");
        writer.WriteLine($"COMPUTERNAME:\t\t{Environment.GetEnvironmentVariables()["COMPUTERNAME"]}");

        foreach (var entry in healthReport.Entries)
        {
            writer.WriteLine();
            writer.Write(entry.Key + '\t');
            writer.Write(entry.Value.Status.ToString() + '\t');
            writer.Write(entry.Value.Description + '\n');
            
            foreach (var item in entry.Value.Data)
            {
                writer.WriteLine($"{item.Key}:\t{item.Value}");
            }

            if (entry.Value.Exception is not null) writer.WriteLine(entry.Value.Exception.Message);
        }
    }

    return context.Response.WriteAsync(Encoding.UTF8.GetString(memoryStream.ToArray()));
}

static string GetHostIps(Uri uri)
{
    var ips = System.Net.Dns.GetHostAddresses(uri.Host);
    return $"{string.Join(',', ips.Select(ip => ip.ToString()).ToArray())}";
}
