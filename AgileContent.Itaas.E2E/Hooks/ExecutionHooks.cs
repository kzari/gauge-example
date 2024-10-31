using System.Net.Http;
using System.Threading.Tasks;
using Gauge.CSharp.Lib;
using Gauge.CSharp.Lib.Attribute;

namespace AgileContent.Itaas.E2E.Hooks;

public class ExecutionHooks {
    private ContainerManager _containerManager = new();
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

        // SuiteDataStore.Add("postmanUrl", "http://localhost:9601");
        // SuiteDataStore.Add("prestigeUrl", "http://localhost:9602");
        // SuiteDataStore.Add("webHookUrl", "https://webhook.site/a44c5ec3-33e4-4f13-8da9-e30a7621c445");
        // SuiteDataStore.Add("webhookExternalUrl", "https://webhook.site/a44c5ec3-33e4-4f13-8da9-e30a7621c445");
        // SuiteDataStore.Add("webhookUuid", "a44c5ec3-33e4-4f13-8da9-e30a7621c445");
        // SuiteDataStore.Add("openSearchHost", "http://localhost:9200");
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