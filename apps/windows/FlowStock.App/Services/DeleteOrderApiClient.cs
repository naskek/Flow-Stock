using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Authentication;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlowStock.App;

public sealed class DeleteOrderApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<DeleteOrderApiCallResult> DeleteAsync(
        ServerCloseClientOptions options,
        long orderId,
        CancellationToken cancellationToken = default)
    {
        Uri baseUri;
        try
        {
            baseUri = new Uri(options.BaseUrl, UriKind.Absolute);
        }
        catch (UriFormatException ex)
        {
            return DeleteOrderApiCallResult.TransportFailure(
                DeleteOrderTransportFailureKind.InvalidConfiguration,
                $"Некорректный адрес сервера: {options.BaseUrl}",
                ex);
        }

        using var handler = CreateHandler(options);
        using var client = new HttpClient(handler)
        {
            BaseAddress = baseUri
        };

        using var responseMessage = await client.DeleteAsync($"/api/orders/{orderId}", cancellationToken)
            .ConfigureAwait(false);

        if (responseMessage.StatusCode == HttpStatusCode.OK)
        {
            var payload = await responseMessage.Content.ReadFromJsonAsync<DeleteOrderApiResponse>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            if (payload == null)
            {
                return DeleteOrderApiCallResult.TransportFailure(
                    DeleteOrderTransportFailureKind.InvalidResponse,
                    "Сервер вернул пустой ответ при удалении заказа.");
            }

            return DeleteOrderApiCallResult.Success(payload);
        }

        var error = await responseMessage.Content.ReadFromJsonAsync<ApiErrorResponse>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return DeleteOrderApiCallResult.HttpError(responseMessage.StatusCode, error);
    }

    private static HttpMessageHandler CreateHandler(ServerCloseClientOptions options)
    {
        var handler = new HttpClientHandler();
        if (options.AllowInvalidTls)
        {
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        return handler;
    }

    public static bool IsTlsFailure(HttpRequestException exception)
    {
        if (exception.InnerException is AuthenticationException)
        {
            return true;
        }

        return exception.Message.Contains("SSL", StringComparison.OrdinalIgnoreCase)
               || exception.Message.Contains("TLS", StringComparison.OrdinalIgnoreCase)
               || exception.Message.Contains("certificate", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class DeleteOrderApiResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }

    [JsonPropertyName("result")]
    public string Result { get; init; } = string.Empty;

    [JsonPropertyName("order_id")]
    public long OrderId { get; init; }

    [JsonPropertyName("order_ref")]
    public string? OrderRef { get; init; }
}

public sealed class DeleteOrderApiCallResult
{
    public DeleteOrderApiResponse? Response { get; init; }
    public ApiErrorResponse? Error { get; init; }
    public HttpStatusCode? StatusCode { get; init; }
    public DeleteOrderTransportFailureKind TransportFailureKind { get; init; }
    public string? TransportErrorMessage { get; init; }
    public Exception? TransportException { get; init; }

    public bool IsTransportFailure => TransportFailureKind != DeleteOrderTransportFailureKind.None;

    public static DeleteOrderApiCallResult Success(DeleteOrderApiResponse response)
    {
        return new DeleteOrderApiCallResult
        {
            Response = response
        };
    }

    public static DeleteOrderApiCallResult HttpError(HttpStatusCode statusCode, ApiErrorResponse? error)
    {
        return new DeleteOrderApiCallResult
        {
            StatusCode = statusCode,
            Error = error
        };
    }

    public static DeleteOrderApiCallResult TransportFailure(
        DeleteOrderTransportFailureKind kind,
        string message,
        Exception? exception = null)
    {
        return new DeleteOrderApiCallResult
        {
            TransportFailureKind = kind,
            TransportErrorMessage = message,
            TransportException = exception
        };
    }
}

public enum DeleteOrderTransportFailureKind
{
    None,
    InvalidConfiguration,
    Timeout,
    Network,
    InvalidResponse,
    Unexpected
}
