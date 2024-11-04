using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AgileContent.Itaas.E2E.Models;
using E2E.Models;
using Gauge.CSharp.Lib;

namespace AgileContent.Itaas.E2E.Steps;

public class TestBase {
    private const string PrometheusCatalogsPath = "catalogs";
    protected static string PostmanUrl => SuiteDataStore.Get<string>("postmanUrl");
    protected static string PrestigeUrl => SuiteDataStore.Get<string>("prestigeUrl");
    protected static string WebHookUrl => SuiteDataStore.Get<string>("webHookUrl");
    protected static string WebhookExternalUrl => SuiteDataStore.Get<string>("webhookExternalUrl");
    protected static string WebhookUuid => SuiteDataStore.Get<string>("webhookUuid");
    protected static string PrometheusUrl => SuiteDataStore.Get<string>("prometheusUrl");

    protected static CatalogRequestBody GetCatalogRequestBody(string bucketName, string key, string instance, string name, string language) {
        var prestigeUrls = "http://" + FormatUrl(PrestigeUrl, "v1", "dump", false);
        return new CatalogRequestBody {
            InstanceId = instance,
            Name = name,
            Language = language,
            CacheDuration = 30,
            AutoActivate = true,
            ServerUrls = new ServerUrls {
                PrestigeUrls = [prestigeUrls],
                StatusUrl = WebHookUrl + "/" + WebhookUuid,
                SpotlightUrl = "http://itaas.spotlight.agilesvcs.com"
            },
            CatalogLocation = new CatalogLocation {
                LocationType = "S3",
                Bucket = bucketName,
                Key = key
            }
        };
    }
    protected static async Task<HttpResponseMessage> PostJsonCatalog(CatalogRequestBody body) {
        var client = new HttpClient{ BaseAddress = new Uri(PostmanUrl) };
        var content = GetStringContentFromObject(body);
        var contentString = await content.ReadAsStringAsync();
        var response = await client.PostAsync("v2/catalogs/json", content);
        var responseContent = await response.Content.ReadAsStringAsync();
        return response;
    }

    protected static WebhookReader GetWebhookReader() => new WebhookReader(new HttpClient(), WebhookExternalUrl);

    protected static StringContent GetStringContentFromObject(object data) =>
        new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");

    protected static async Task<(bool, TimeSpan)> WaitProcessComplete(WebhookReader webhookReader) {
        var lastReportTimeout = TimeSpan.FromMilliseconds(200_000);
        var isFinished = await RetryWrapper.Retry(500_000, () =>
            webhookReader.IsProcessFinished(WebhookUuid, lastReportTimeout));
        return (isFinished, lastReportTimeout);
    }

    protected static async Task<bool> FindCatalogInPrometheus(HttpClient client, string instanceId, List<string> databasesNames) {
        var timeout = 20_000;
        var catalogFound = await RetryWrapper.Retry(timeout, async () =>
            (await GetCatalogsDatabasesFromPrometheus(client, instanceId))
            .Any(publishedDb => databasesNames.Any(dbName => dbName == publishedDb)));
        return catalogFound;
    }

    protected static async Task<IEnumerable<string>> GetCatalogsDatabasesFromPrometheus(HttpClient client, string instanceId) {
        var uri = FormatUrl(PrometheusUrl, "v2", PrometheusCatalogsPath) + "?noCache=" + Guid.NewGuid();
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        return (await client.SendAsync(request))
            .GetRootJsonElement()
            .Result
            .GetJsonElementList(instanceId)
            .GetListOfValuesIfPropertyExists("Database")
            .Select(e => e.ToString());
    }

    protected static async Task<IEnumerable<JsonElement>> GetContentListFromResponse(HttpResponseMessage response) {
        var responseString = await response.Content.ReadAsStringAsync();
        var jsonResponse = JsonDocument.Parse(responseString);
        var contents = jsonResponse.RootElement.GetProperty("Content");
        return contents.GetProperty("List").EnumerateArray();
    }

    private static string FormatUrl(string root, string version, string path, bool isHotDelta = false, bool isUpsert = false) {
        var url = $"{root}/{version}/{path}";
        var keyValues = new Dictionary<string, string>();
        if (isHotDelta) keyValues.Add("hotdelta", "true");
        if (isUpsert) keyValues.Add("upsert", "true");
        var parameters = keyValues.Select(x => $"{x.Key}={x.Value}");
        var queryString = "?" + string.Join("&", parameters);
        return url + (isHotDelta || isUpsert ? queryString : "");
    }
}
