using Azure.Identity;
using Azure.Messaging.ServiceBus.Administration;
using HealthChecksAspNet;
using HealthChecksCommon;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net;
using System.Text;
using AzureCacheRedisClient;
using static HealthChecksCommon.Constants;


var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

//builder.Services.AddHealthChecksDotNet(config);
builder.Services.AddTransient<HealthChecksService>();

builder.Services.AddAzureClients(builder =>
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
    builder.Services.AddSingleton(new ServiceBusAdministrationClient(config[AzureServiceBusFQNamespace], new DefaultAzureCredential()));
}

// SQL SERVER
if (!string.IsNullOrWhiteSpace(config[SqlServerConnectionString]))
{
    builder.Services.AddSingleton(new SqlConnection(config[SqlServerConnectionString]));
}

// REDIS CACHE
if (!string.IsNullOrWhiteSpace(config[RedisConnectionString]))
{
    builder.Services.AddSingleton(new RedisDb(config[RedisConnectionString]));
}

builder.Services.AddApplicationInsightsTelemetry();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

//app.MapHealthChecks("/health", new HealthCheckOptions
//{
//    ResponseWriter = HealthChecksDotNetResponseWriter.WriteResponse
//});

//app.UseHealthChecks("/", new HealthCheckOptions
//{
//    Predicate = r => r.Name.Contains("self"),
//    ResponseWriter = HealthChecksDotNetResponseWriter.WriteResponse
//});

app.MapGet("/health", async (HealthChecksService healthChecksService) => 
{
    var healthReport = await healthChecksService.RunHealthChecks();

    return Results.Extensions.StatusCodeText(
        HealthChecksService.HttpStatusFromHealthCheckStatus(healthReport), 
        HealthChecksDotNetResponseWriter.WriteResponseString(healthReport));
});

app.MapGet("/", () =>
{
    return "ok. GET /health for Health Checks";
});

app.MapGet("/503", () =>
{
    return Results.StatusCode((int)HttpStatusCode.ServiceUnavailable);
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

        return Results.Extensions.StatusCodeText(response.StatusCode,
            $"RESPONSE STATUS {response.StatusCode}\n\n{await response.Content.ReadAsStringAsync()}");
    }
});

app.Run();
