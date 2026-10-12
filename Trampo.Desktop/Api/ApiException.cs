using System.Net;

namespace Trampo.Desktop.Api;

internal sealed class ApiException(string message, HttpStatusCode? statusCode = null) : Exception(message)
{
    public HttpStatusCode? StatusCode { get; } = statusCode;
    public bool IsUnauthorized => StatusCode == HttpStatusCode.Unauthorized;
}
