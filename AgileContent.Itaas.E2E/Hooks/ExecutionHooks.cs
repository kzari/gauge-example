using System.Net.Http;
using System.Threading.Tasks;
using Gauge.CSharp.Lib;
using Gauge.CSharp.Lib.Attribute;

namespace AgileContent.Itaas.E2E.Hooks;

public class ExecutionHooks {
    private readonly ContainerManager _containerManager = new();
    private const string GetWebhookUuidPath = "/token";
    protected readonly HttpClient _client = new();

    [BeforeSuite]
    public async Task BeforeSuite() {
        var containersInfo = await _containerManager.StartAsync();

        SuiteDataStore.Add("postmanUrl", containersInfo.PostmanUrl);
        SuiteDataStore.Add("prestigeUrl", containersInfo.PrestigeUrl);
        SuiteDataStore.Add("prometheusUrl", containersInfo.PrometheusUrl);
        SuiteDataStore.Add("webHookUrl", containersInfo.WebhookAlias);
        SuiteDataStore.Add("webhookExternalUrl", containersInfo.WebhookHost);
        SuiteDataStore.Add("webhookUuid", await GetExternalWebhookUuid(containersInfo.WebhookHost));
        SuiteDataStore.Add("openSearchHost", containersInfo.OpenSearchHost);
    }

    [AfterSuite]
    public async Task AfterSuite() {
        await _containerManager.DestroyAsync();
    }

    protected async Task<string> GetExternalWebhookUuid(string webhookExternalUrl) {
        var uri = $"{webhookExternalUrl}{GetWebhookUuidPath}";
        _client.DefaultRequestHeaders.Add("Accept", "application/json");
        _client.DefaultRequestHeaders.Add("User-Agent", "E2E");
        var request = new HttpRequestMessage(HttpMethod.Post, uri);
        var response = await _client.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();
        return (await response.GetRootJsonElement()).GetProperty("uuid").GetString();
    }
}