using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using E2E.Models;
using FluentAssertions;
using Gauge.CSharp.Lib.Attribute;

namespace AgileContent.Itaas.E2E.Steps;

public class StepImplementation : TestBase {
    private const string GetCatalogsFailMessage = "Database {0} not found in Prometheus.";
    private const string ProcessTimeoutMessage = "Catalog didn't finish process after {0}ms";
    private CatalogRequestBody _catalogRequestBody;
    private HttpResponseMessage _postmanPostResponse;
    private HttpResponseMessage _prometheusGetResponse;

    [Step("Given S3 buket is <bucketName>, key is <key>, instance is <instance>, name is <name>, language is <language>.")]
    public void GivenCatalogDetails(string bucketName, string key, string instance, string name, string language) {
        _catalogRequestBody = GetCatalogRequestBody(bucketName, key, instance, name, language);
    }

    [Step("Post Json Catalog on Postman.")]
    public async Task PostJsonCatalog() {
        _postmanPostResponse = await PostJsonCatalog(_catalogRequestBody);
    }

    [Step("Assert the catalog db instanceId <instanceId> exists on Prometheus.")]
    public async Task AssertCatalogDbExists(string instanceId) {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "E2E");
        var webhookReader = GetWebhookReader();
        var (isFinished, lastReportTimeout) = await WaitProcessComplete(webhookReader);
        isFinished.Should().BeTrue(string.Format($"{ProcessTimeoutMessage} {WebhookUuid}", lastReportTimeout));

        var currentDatabaseName = await webhookReader.GetDatabaseName(WebhookUuid);
        var catalogFound = await FindCatalogInPrometheus(client, instanceId, [currentDatabaseName]);
        catalogFound.Should().BeTrue(string.Format(GetCatalogsFailMessage, currentDatabaseName));
    }

    [Step("Assert Post result is 200.")]
    public void AssertPostResultIs200() {
        _postmanPostResponse.StatusCode.Should().Be(HttpStatusCode.OK, _postmanPostResponse.Content.ReadAsStringAsync().Result);
    }

    [Step("Call prometheus on <uri>.")]
    public async Task CallPrometheus(string uri) {
        var webhookReader = GetWebhookReader();
        var (isFinished, lastReportTimeout) = await WaitProcessComplete(webhookReader);
        isFinished.Should().BeTrue(string.Format($"{ProcessTimeoutMessage} {WebhookUuid}", lastReportTimeout));

        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.CacheControl = CacheControlHeaderValue.Parse("no-cache");
        uri = PrometheusUrl + "/v2/01/json-catalog/pt-br/" + uri;
        _prometheusGetResponse = await httpClient.GetAsync(uri);
    }

    [Step("Assert the content pid list contains <pids>.")]
    public async Task AssertTheContentPidListContains(string pids) {
        var expectedPids = pids.Split(',');
        var contentList = await GetContentListFromResponse(_prometheusGetResponse);
        var pidsList = contentList.Select(c => c.GetProperty("Pid").GetString());
        pidsList.Should().Contain(expectedPids);
    }
}