using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using E2E.Models;
using FluentAssertions;
using Gauge.CSharp.Lib.Attribute;

namespace AgileContent.Itaas.E2E.Steps;

public class StepImplementation : TestBase {
    protected CatalogRequestBody _catalogRequestBody;
    protected HttpResponseMessage _postmanPostResponse;
    protected HttpResponseMessage _prometheusGetResponse;

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

        var currentDatabaseName = await GetCatalogDatabaseNameAfterProcessFinish(client, WebhookUuid);

        await WaitCatalogToAppearInPrometheus(client, instanceId, [currentDatabaseName]);
    }

    [Step("Assert Post result is 200.")]
    public void AssertPostResultIs200() {
        _postmanPostResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Step("Call prometheus on <uri>.")]
    private async Task CallPrometheus(string uri) {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.CacheControl = CacheControlHeaderValue.Parse("no-cache");
        // if (!string.IsNullOrEmpty(_queryString)) {
        //     uri += _queryString;
        // }
        uri = PrometheusUrl + "v2/01/json-catalog/pt-br" + uri;
        _prometheusGetResponse = await httpClient.GetAsync(uri);
    }

    [Step("Assert the content pid list is <pids>.")]
    public async Task ThenTheContentListShouldHaveExactly(string pids) {
        var expectedPidsList = pids.Split(',');
        var contentList = await GetContentListFromResponse(_prometheusGetResponse);
        var pidsList = contentList.Select(c => c.GetProperty("Pid").GetString());
        expectedPidsList.Should().BeEquivalentTo(pidsList);
    }

    
    private static async Task<IEnumerable<JsonElement>> GetContentListFromResponse(HttpResponseMessage response) {
        var responseString = await response.Content.ReadAsStringAsync();
        var jsonResponse = JsonDocument.Parse(responseString);
        var contents = jsonResponse.RootElement.GetProperty("Content");
        return contents.GetProperty("List").EnumerateArray();
    }
}