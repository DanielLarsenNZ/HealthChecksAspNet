using HealthChecksAspNet;
using HealthChecksCommon;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Net;
using System.Text;
using static HealthChecksCommon.Constants;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.AddHealthChecksDotNet(config);

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthChecksDotNetResponseWriter.WriteResponse
});

app.UseHealthChecks("/", new HealthCheckOptions
{
    Predicate = r => r.Name.Contains("self"),
    ResponseWriter = HealthChecksDotNetResponseWriter.WriteResponse
});

app.MapGet("/hello", () =>
{
    return "hello";
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
