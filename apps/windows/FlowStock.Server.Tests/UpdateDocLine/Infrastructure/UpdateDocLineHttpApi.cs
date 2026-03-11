using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlowStock.Server;

namespace FlowStock.Server.Tests.UpdateDocLine.Infrastructure;

internal static class UpdateDocLineHttpApi
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<UpdateDocLineEnvelope> UpdateAsync(HttpClient client, string docUid, UpdateDocLineRequest request)
    {
        using var response = await client.PostAsJsonAsync($"/api/docs/{docUid}/lines/update", request);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<UpdateDocLineEnvelope>(JsonOptions);
        Assert.NotNull(payload);
        return payload!;
    }

    public static async Task<ApiResult> ReadApiResultAsync(HttpResponseMessage response, HttpStatusCode expectedStatusCode)
    {
        Assert.Equal(expectedStatusCode, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiResult>(JsonOptions);
        Assert.NotNull(payload);
        return payload!;
    }
}

internal sealed class UpdateDocLineEnvelope
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
    public UpdateDocLinePayload? Line { get; init; }
}

internal sealed class UpdateDocLinePayload
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
