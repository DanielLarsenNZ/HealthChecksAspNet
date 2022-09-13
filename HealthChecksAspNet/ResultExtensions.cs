using System.Net;
using System.Net.Mime;
using System.Text;

namespace HealthChecksAspNet
{
    internal class StatusCodeTextResult : IResult
    {
        private readonly string _body;
        private readonly HttpStatusCode _statusCode;

        public StatusCodeTextResult(HttpStatusCode statusCode, string bodyText)
        {
            _statusCode = statusCode;
            _body = bodyText;
        }

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.ContentType = MediaTypeNames.Text.Plain;
            httpContext.Response.ContentLength = Encoding.UTF8.GetByteCount(_body);
            httpContext.Response.StatusCode = (int)_statusCode;
            await httpContext.Response.WriteAsync(_body);
        }
    }

    internal static class ResultExtensions
    {
        public static IResult StatusCodeText(this IResultExtensions extensions, HttpStatusCode status, string html) 
            => new StatusCodeTextResult(status, html);
    }
}
