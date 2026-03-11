using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Authentication;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlowStock.App;

public sealed class DeleteDocLineApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<DeleteDocLineApiCallResult> DeleteAsync(
        ServerCloseClientOptions options,
        string docUid,
        DeleteDocLineApiRequest request,
        CancellationToken cancellationToken = default)
    {
        Uri baseUri;
        try
        {
            baseUri = new Uri(options.BaseUrl, UriKind.Absolute);
        }
        catch (UriFormatException ex)
        {
            return DeleteDocLineApiCallResult.TransportFailure(
                DeleteDocLineTransportFailureKind.InvalidConfiguration,
                $"Некорректный адрес сервера: {options.BaseUrl}",
                ex);
        }

        using var handler = CreateHandler(options);
        using var client = new HttpClient(handler)
        {
            BaseAddress = baseUri
        };

        using var responseMessage = await client.PostAsJsonAsync(
            $"/api/docs/{Uri.EscapeDataString(docUid)}/lines/delete",
            request,
            cancellationToken);

        if (responseMessage.StatusCode == HttpStatusCode.OK)
        {
            var payload = await responseMessage.Content.ReadFromJsonAsync<DeleteDocLineApiResponse>(JsonOptions, cancellationToken);
            if (payload == null)
            {
                return DeleteDocLineApiCallResult.TransportFailure(
                    DeleteDocLineTransportFailureKind.InvalidResponse,
                    "Сервер вернул пустой ответ при удалении строки.");
            }

            return DeleteDocLineApiCallResult.Success(payload);
        }

        var error = await responseMessage.Content.ReadFromJsonAsync<ApiErrorResponse>(JsonOptions, cancellationToken);
        return DeleteDocLineApiCallResult.HttpError(responseMessage.StatusCode, error);
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

public sealed class DeleteDocLineApiRequest
{
    [JsonPropertyName("event_id")]
    public string EventId { get; init; } = string.Empty;

    [JsonPropertyName("device_id")]
    public string? DeviceId { get; init; }

    [JsonPropertyName("line_id")]
    public long LineId { get; init; }
}

public sealed class DeleteDocLineApiResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }

    [JsonPropertyName("result")]
    public string Result { get; init; } = string.Empty;

    [JsonPropertyName("doc_uid")]
    public string? DocUid { get; init; }

    [JsonPropertyName("doc_status")]
    public string? DocStatus { get; init; }

    [JsonPropertyName("appended")]
    public bool Appended { get; init; }

    [JsonPropertyName("idempotent_replay")]
    public bool IdempotentReplay { get; init; }

    [JsonPropertyName("line")]
    public DeleteDocLineApiPayload? Line { get; init; }
}

public sealed class DeleteDocLineApiPayload
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("replaces_line_id")]
    public long? ReplacesLineId { get; init; }

    [JsonPropertyName("item_id")]
    public long ItemId { get; init; }

    [JsonPropertyName("qty")]
    public double Qty { get; init; }

    [JsonPropertyName("uom_code")]
    public string? UomCode { get; init; }

    [JsonPropertyName("order_line_id")]
    public long? OrderLineId { get; init; }

    [JsonPropertyName("from_location_id")]
    public long? FromLocationId { get; init; }

    [JsonPropertyName("to_location_id")]
    public long? ToLocationId { get; init; }

    [JsonPropertyName("from_hu")]
    public string? FromHu { get; init; }

    [JsonPropertyName("to_hu")]
    public string? ToHu { get; init; }
}

public sealed class DeleteDocLineApiCallResult
{
    public DeleteDocLineApiResponse? Response { get; init; }
    public ApiErrorResponse? Error { get; init; }
    public HttpStatusCode? StatusCode { get; init; }
    public DeleteDocLineTransportFailureKind TransportFailureKind { get; init; }
    public string? TransportErrorMessage { get; init; }
    public Exception? TransportException { get; init; }

    public bool IsTransportFailure => TransportFailureKind != DeleteDocLineTransportFailureKind.None;

    public static DeleteDocLineApiCallResult Success(DeleteDocLineApiResponse response)
    {
        return new DeleteDocLineApiCallResult
        {
            Response = response
        };
    }

    public static DeleteDocLineApiCallResult HttpError(HttpStatusCode statusCode, ApiErrorResponse? error)
    {
        return new DeleteDocLineApiCallResult
        {
            StatusCode = statusCode,
            Error = error
        };
    }

    public static DeleteDocLineApiCallResult TransportFailure(
        DeleteDocLineTransportFailureKind kind,
        string message,
        Exception? exception = null)
    {
        return new DeleteDocLineApiCallResult
        {
            TransportFailureKind = kind,
            TransportErrorMessage = message,
            TransportException = exception
        };
    }
}

public enum DeleteDocLineTransportFailureKind
{
    None,
    InvalidConfiguration,
    Timeout,
    Network,
    InvalidResponse,
    Unexpected
}
