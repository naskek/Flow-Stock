using System.Net;
using System.Net.Http.Json;
using FlowStock.Server;

namespace FlowStock.Server.Tests.DeleteOrder.Infrastructure;

internal static class DeleteOrderHttpApi
{
    public static async Task<DeleteOrderEnvelope> DeleteAsync(HttpClient client, long orderId)
    {
        using var response = await client.DeleteAsync($"/api/orders/{orderId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<DeleteOrderEnvelope>();
        return Assert.IsType<DeleteOrderEnvelope>(payload);
    }

    public static async Task<ApiResult> ReadApiResultAsync(HttpResponseMessage response, HttpStatusCode expectedStatusCode)
    {
        Assert.Equal(expectedStatusCode, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiResult>();
        return Assert.IsType<ApiResult>(payload);
    }
}
