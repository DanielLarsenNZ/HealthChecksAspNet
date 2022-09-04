﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace HealthChecksCommon
{
    public static class HealthChecksDotNetResponseWriter
    {
        public static Task WriteResponse(HttpContext context, HealthReport healthReport) =>
            context.Response.WriteAsync(WriteResponseStringHelper(healthReport, context));

        public static string WriteResponseString(HealthReport healthReport) =>
            WriteResponseStringHelper(healthReport);

        private static string WriteResponseStringHelper(
            HealthReport healthReport,
            HttpContext? context = null)
        {
            using var memoryStream = new MemoryStream();
            using (var writer = new StreamWriter(memoryStream))
            {
                writer.WriteLine(healthReport.Status.ToString().ToUpper());

                if (context is not null) writer.WriteLine($"Request.Host:\t\t{context.Request.Host}");
                if (context is not null) writer.WriteLine($"RemoteIpAddress:\t{context.Connection.RemoteIpAddress}");

                if (context is not null)
                {
                    var httpConnectionFeature = context.Request.HttpContext.Features.Get<IHttpConnectionFeature>();
                    writer.WriteLine($"LocalIpAddress:\t\t{httpConnectionFeature?.LocalIpAddress}");
                }
                writer.WriteLine($"UtcNow:\t\t\t{DateTime.UtcNow}");
                writer.WriteLine($"WEBSITE_INSTANCE_ID:\t{Environment.GetEnvironmentVariables()["WEBSITE_INSTANCE_ID"]}");
                writer.WriteLine($"COMPUTERNAME:\t\t{Environment.GetEnvironmentVariables()["COMPUTERNAME"]}");
                writer.WriteLine($"HOSTNAME:\t\t{Environment.GetEnvironmentVariables()["HOSTNAME"]}\n");
                writer.WriteLine($"WEBSITE_PRIVATE_IP:\t{Environment.GetEnvironmentVariables()["WEBSITE_PRIVATE_IP"]}\n");

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

            return Encoding.UTF8.GetString(memoryStream.ToArray());
        }
    }
}