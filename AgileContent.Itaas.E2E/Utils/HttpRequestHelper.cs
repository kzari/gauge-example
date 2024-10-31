using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace AgileContent.Itaas.E2E;

public static class HttpRequestHelper
{
    public static async Task<JsonElement> GetRootJsonElement(this HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Response code is NOT a success status code {response.StatusCode}, with contents: {content}");
        }

        return JsonDocument.Parse(content).RootElement;
    }
}