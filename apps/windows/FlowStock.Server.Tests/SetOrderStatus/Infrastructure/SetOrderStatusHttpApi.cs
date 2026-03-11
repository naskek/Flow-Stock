using System.Net;
using System.Net.Http.Json;
using FlowStock.Server;

namespace FlowStock.Server.Tests.SetOrderStatus.Infrastructure;

internal static class SetOrderStatusHttpApi
{
    public static async Task<SetOrderStatusEnvelope> ChangeAsync(HttpClient client, long orderId, string status)
    {
        using var response = await client.PostAsJsonAsync($"/api/orders/{orderId}/status", new SetOrderStatusRequest
        {
            Status = status
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<SetOrderStatusEnvelope>();
        return Assert.IsType<SetOrderStatusEnvelope>(payload);
    }

    public static async Task<ApiResult> ReadApiResultAsync(HttpResponseMessage response, HttpStatusCode expectedStatusCode)
    {
        Assert.Equal(expectedStatusCode, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiResult>();
        return Assert.IsType<ApiResult>(payload);
    }
}
