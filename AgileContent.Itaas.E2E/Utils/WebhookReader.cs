using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using AgileContent.Itaas.E2E.Models;

namespace AgileContent.Itaas.E2E;

public class WebhookReader {
    private readonly string _webhookUrl;
    private readonly HttpClient _client;
    private const string GetRequestsPath = "/token/{0}/requests";
    private const string GetWebhookUuidPath = "/token";

    public WebhookReader(HttpClient client, string webhookExternalUrl) {
        _client = client;
        _webhookUrl = webhookExternalUrl;
    }

    public async Task<string> GetDatabaseName(string uuid) {
        var listOfExistingValues = await ParseDatabasesFromRequest(uuid);
        var lastDatabase = listOfExistingValues.Select(x => x.DatabaseName)
            .LastOrDefault();
        return lastDatabase;
    }

    public async Task<List<string>> GetDatabasesNames(string uuid) {
        var listOfExistingValues = await ParseDatabasesFromRequest(uuid);
        return listOfExistingValues
            .OrderByDescending(x => x.Date)
            .Select(x => x.DatabaseName)
            .ToList();
    }

    private async Task<IEnumerable<Notification>> ParseDatabasesFromRequest(string uuid) {
        var listOfRequests = await GetAllRequests(uuid);
        var listOfContents = GetRequestsContentList(listOfRequests);
        return listOfContents.Where(x => !string.IsNullOrEmpty(x.DatabaseName));
    }

    public async Task<bool> IsProcessFinished(string uuid, TimeSpan timeout) {
        var notifications = await GetAllRequestsStatuses(uuid);

        foreach (var notification in notifications)
        {
            Console.WriteLine(notification);
        }

        HasErrorStatus(notifications);
        CheckLastStatusTime(notifications, timeout);
        var finishedOccurrences = notifications.Count(c => c.Status.Equals(WebhookProcessStatus.Finished));
        if (finishedOccurrences > 0)
            return true;

        var infoNotification = notifications.Where(c => c.Status.Equals(WebhookProcessStatus.Info));
        return infoNotification.Any(n => n.Message.ToLower() == "database updated" ||
                                         n.Message.ToLower() == "database created");
    }

    public async Task<bool> IsActiveted(string uuid, TimeSpan timeout) {
        var notifications = await GetAllRequestsStatuses(uuid);

        foreach (var notification in notifications)
        {
            Console.WriteLine(notification);
        }

        HasErrorStatus(notifications);
        CheckLastStatusTime(notifications, timeout);
        return notifications.Any(x =>
        {
            return x.Status.Equals(WebhookProcessStatus.OldCatalogsInactivated);
        });
    }


    private static void HasErrorStatus(IEnumerable<Notification> requests) {
        var errorRequest = requests.FirstOrDefault(r =>
        {
            return r.Status.Equals(WebhookProcessStatus.Error);
        });

        if (errorRequest == default) return;
        throw new Exception($"Request errored with message: {errorRequest}");
    }

    private static void CheckLastStatusTime(List<Notification> requests, TimeSpan timeout) {
        var maxTime = requests.Select(x => x.Date)
            .OrderByDescending(x => x)
            .FirstOrDefault();

        if (maxTime != default && DateTime.UtcNow - maxTime > timeout)
            throw new Exception($"Didn't receive any status report in the last {timeout.TotalSeconds} seconds");
    }

    private async Task<List<Notification>> GetAllRequestsStatuses(string uuid) =>
        GetAllStatuses(await GetAllRequests(uuid));

    private HttpRequestMessage GetMessageForGetRequests(string uuid) =>
        new(HttpMethod.Get, string.Format(_webhookUrl + GetRequestsPath, uuid));

    private async Task<IEnumerable<JsonElement>> GetAllRequests(string uuid) {
        var request = GetMessageForGetRequests(uuid);
        var response = await _client.SendAsync(request);
        return (await response.GetRootJsonElement()).GetJsonElementList("data");
    }

    protected async Task<string> GetExternalWebhookUuid(string webhookExternalUrl) {
        var client = new HttpClient();
        var uri = $"{webhookExternalUrl}{GetWebhookUuidPath}";
        var response = await client.PostAsync(uri, null);
        return (await response.GetRootJsonElement()).GetProperty("uuid").GetString();
    }
    
    private static IEnumerable<Notification> GetRequestsContentList(IEnumerable<JsonElement> allRequests) {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var contents = allRequests.Select(GetContentStringFromRequest);
        var deserealizedContents = contents
            .Where(c => !string.IsNullOrEmpty(c))
            .Select(c => JsonSerializer.Deserialize<Notification>(c, options))
            .ToList();
        return deserealizedContents;
    }

    private static List<Notification> GetAllStatuses(IEnumerable<JsonElement> requests) =>
        GetRequestsContentList(requests).ToList();

    private static string GetContentStringFromRequest(JsonElement element) =>
        element.GetProperty("content").GetString();
}