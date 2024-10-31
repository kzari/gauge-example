using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AgileContent.Itaas.E2E.Models;
using E2E.Models;
using FluentAssertions;
using Gauge.CSharp.Lib;
using RestSharp;

namespace AgileContent.Itaas.E2E.Steps;

public class TestBase {
    private const string ProcessTimeoutMessage = "Catalog didn't finish process after {0}ms";
    private const string GetCatalogsFailMessage = "Database {0} not found in Prometheus after {1}ms.";
    private const string PrometheusCatalogsPath = "catalogs";
    protected string PostmanUrl => SuiteDataStore.Get<string>("postmanUrl");
    protected string PrestigeUrl => SuiteDataStore.Get<string>("prestigeUrl");
    protected string WebHookUrl => SuiteDataStore.Get<string>("webHookUrl");
    protected string WebhookExternalUrl => SuiteDataStore.Get<string>("webhookExternalUrl");
    protected string WebhookUuid => SuiteDataStore.Get<string>("webhookUuid");
    protected string PrometheusUrl => SuiteDataStore.Get<string>("prometheusUrl");

    protected CatalogRequestBody GetCatalogRequestBody(string bucketName, string key, string instance, string name, string language) {
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
    protected async Task<RestResponse> PostJsonCatalogOLD(CatalogRequestBody body) {
        var client = new RestClient(PostmanUrl);
        var request = new RestRequest("v1/catalogs/json", Method.Post);
        request.AddJsonBody(body);
        return await client.PostAsync(request);
    }
    protected async Task<HttpResponseMessage> PostJsonCatalog(CatalogRequestBody body) {
        var client = new HttpClient{ BaseAddress = new Uri(PostmanUrl) };
        var content = GetStringContentFromObject(body);
        var contentString = await content.ReadAsStringAsync();
        var response = await client.PostAsync("v2/catalogs/json", content);
        var responseContent = await response.Content.ReadAsStringAsync();
        return response;
    }

    protected async Task<string> GetCatalogDatabaseNameAfterProcessFinish(HttpClient client, string webHookUuid) {
        var webhookReader = new WebhookReader(client, WebhookExternalUrl);
        await WaitCatalogProcessFinished(webhookReader, webHookUuid);
        return await webhookReader.GetDatabaseName(webHookUuid);
    }
    protected static StringContent GetStringContentFromObject(object data) =>
        new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");

    private static async Task WaitCatalogProcessFinished(WebhookReader webhookReader, string webHookUuid) {
        var lastReportTimeout = TimeSpan.FromMilliseconds(200_000);
        var isFinished = await RetryWrapper.Retry(500_000, () =>
            webhookReader.IsProcessFinished(webHookUuid, lastReportTimeout));

        isFinished.Should().BeTrue(string.Format($"{ProcessTimeoutMessage} {webHookUuid}", lastReportTimeout));
    }

    protected async Task WaitCatalogToAppearInPrometheus(HttpClient client, string instanceId, List<string> databasesNames) {
        var timeout = 20_000;

        var catalogFound = await RetryWrapper.Retry(timeout, async () =>
            (await GetCatalogsDatabasesFromPrometheus(client, instanceId, PrometheusUrl))
            .Any(publishedDb => databasesNames.Any(dbName => dbName == publishedDb)));

        catalogFound.Should().BeTrue(string.Format(GetCatalogsFailMessage, databasesNames, timeout));
    }


    private static async Task<IEnumerable<string>> GetCatalogsDatabasesFromPrometheus(HttpClient client, string instanceId, string prometheusUrl) {
        var guid = Guid.NewGuid();

        var uri = FormatUrl(prometheusUrl, "v2", PrometheusCatalogsPath) + "?noCache=" + guid;
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        return (await client.SendAsync(request))
            .GetRootJsonElement()
            .Result
            .GetJsonElementList(instanceId)
            .GetListOfValuesIfPropertyExists("Database")
            .Select(e => e.ToString());
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
