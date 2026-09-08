using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using WioLinkWebApp.Resources;

namespace WioLinkWebApp.Services;

public class WioLinkService
{
    private const string GroveImagePrefix = "http://bazaar.seeed.cc/";
    private readonly IHttpClientFactory httpClientFactory;

    public WioLinkService(IHttpClientFactory httpClientFactory)
    {
        this.httpClientFactory = httpClientFactory;
    }

    public string? AccessToken { get; private set; }

    public string? ServerBaseAddress { get; private set; }

    public bool IsLoggedIn => !string.IsNullOrEmpty(AccessToken);

    public void ClearToken()
    {
        AccessToken = null;
    }

    public async Task<(bool Success, string? ErrorMessage)> LoginAsync(string email, string password, string serverBaseUrl)
    {
        ServerBaseAddress = NormalizeServerBaseAddress(serverBaseUrl);

        try
        {
            var client = CreateWioServerClient();
            var response = await client.PostAsJsonAsync("v1/user/login", new
            {
                email,
                password
            });

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
                if (!string.IsNullOrEmpty(result?.Token))
                {
                    AccessToken = result.Token;
                    return (true, null);
                }

                return (false, WioLinkServiceMessages.LoginFailed);
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return (false, WioLinkServiceMessages.InvalidCredentials);
            }

            return (false, string.Format(WioLinkServiceMessages.HttpStatusError1, (int)response.StatusCode));
        }
        catch
        {
            return (false, WioLinkServiceMessages.ServerConnectionFailed);
        }
    }

    public async Task<(List<NodeItem>? Nodes, string? ErrorMessage, bool Unauthorized)> GetNodesAsync()
    {
        if (!IsLoggedIn)
        {
            return (null, WioLinkServiceMessages.LoginRequired, true);
        }

        try
        {
            var client = CreateWioServerClient();
            var token = Uri.EscapeDataString(AccessToken!);
            var response = await client.GetAsync($"v1/nodes/list?access_token={token}");

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<NodesListResponse>();
                return (result?.Nodes ?? new List<NodeItem>(), null, false);
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                ClearToken();
                return (null, WioLinkServiceMessages.AuthenticationFailed, true);
            }

            return (null, string.Format(WioLinkServiceMessages.GetNodesFailedWithStatus1, (int)response.StatusCode), false);
        }
        catch
        {
            return (null, WioLinkServiceMessages.ServerConnectionFailed, false);
        }
    }

    public async Task<(bool Success, string? ErrorMessage, bool Unauthorized)> RenameNodeAsync(string nodeSn, string name)
    {
        if (!IsLoggedIn)
        {
            return (false, WioLinkServiceMessages.LoginRequired, true);
        }

        try
        {
            var client = CreateWioServerClient();
            var token = Uri.EscapeDataString(AccessToken!);
            var response = await client.PostAsync(
                $"v1/nodes/rename?access_token={token}",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["node_sn"] = nodeSn,
                    ["name"] = name
                }));

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                ClearToken();
                return (false, WioLinkServiceMessages.AuthenticationFailed, true);
            }

            if (!response.IsSuccessStatusCode)
            {
                return (false, string.Format(WioLinkServiceMessages.HttpStatusError1, (int)response.StatusCode), false);
            }

            return (true, null, false);
        }
        catch
        {
            return (false, WioLinkServiceMessages.ServerConnectionFailed, false);
        }
    }

    public async Task<(List<GroveDriverItem>? Drivers, string? ErrorMessage, bool Unauthorized)> GetGroveDriversAsync()
    {
        if (!IsLoggedIn)
        {
            return (null, WioLinkServiceMessages.LoginRequired, true);
        }

        try
        {
            var client = CreateWioServerClient();
            var token = Uri.EscapeDataString(AccessToken!);
            var response = await client.GetAsync($"v1/scan/drivers?access_token={token}");

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<GroveDriverListResponse>();
                var drivers = (result?.Drivers ?? new List<GroveDriverItem>())
                    .Select(driver =>
                    {
                        driver.ImageUrl = ResolveGroveImageUrl(driver.ImageUrl);
                        return driver;
                    })
                    .ToList();

                return (drivers, null, false);
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                ClearToken();
                return (null, WioLinkServiceMessages.AuthenticationFailed, true);
            }

            return (null, string.Format(WioLinkServiceMessages.HttpStatusError1, (int)response.StatusCode), false);
        }
        catch
        {
            return (null, WioLinkServiceMessages.ServerConnectionFailed, false);
        }
    }

    public async Task<(string? RawHtml, string? ErrorMessage, bool Unauthorized)> GetDeviceResourcesAsync(string nodeKey)
    {
        if (!IsLoggedIn)
        {
            return (null, WioLinkServiceMessages.LoginRequired, true);
        }

        try
        {
            var client = CreateWioServerClient();
            var token = Uri.EscapeDataString(nodeKey);
            var response = await client.GetAsync($"v1/node/resources?access_token={token}");

            if (response.IsSuccessStatusCode)
            {
                var raw = await response.Content.ReadAsStringAsync();
                return (raw, null, false);
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return (null, WioLinkServiceMessages.AuthenticationFailed, true);
            }

            return (null, string.Format(WioLinkServiceMessages.HttpStatusError1, (int)response.StatusCode), false);
        }
        catch
        {
            return (null, WioLinkServiceMessages.ServerConnectionFailed, false);
        }
    }

    public async Task<(List<OtaConnectionItem>? Connections, string? ErrorMessage, bool Unauthorized)> GetNodeConfigAsync(string nodeKey)
    {
        if (!IsLoggedIn)
        {
            return (null, WioLinkServiceMessages.LoginRequired, true);
        }

        try
        {
            var client = CreateWioServerClient();
            var token = Uri.EscapeDataString(nodeKey);
            var response = await client.GetAsync($"v1/node/config?access_token={token}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var connections = ExtractNodeConfigConnections(content);
                return (connections, null, false);
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return (null, WioLinkServiceMessages.AuthenticationFailed, true);
            }

            return (null, string.Format(WioLinkServiceMessages.HttpStatusError1, (int)response.StatusCode), false);
        }
        catch
        {
            return (null, WioLinkServiceMessages.ServerConnectionFailed, false);
        }
    }

    public async Task<(bool Success, string? ErrorMessage, bool Unauthorized)> TriggerOtaAsync(
        string nodeKey,
        string boardName,
        IEnumerable<OtaConnectionItem> connections,
        Func<string, Task>? statusChanged = null)
    {
        if (!IsLoggedIn)
        {
            return (false, WioLinkServiceMessages.LoginRequired, true);
        }

        try
        {
            var client = CreateWioServerClient();
            var token = Uri.EscapeDataString(nodeKey);
            var response = await client.PostAsJsonAsync($"v1/ota/trigger?access_token={token}", new OtaTriggerRequest
            {
                BoardName = boardName,
                Connections = connections.ToList()
            });

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return (false, WioLinkServiceMessages.AuthenticationFailed, true);
            }

            if (!response.IsSuccessStatusCode)
            {
                return (false, string.Format(WioLinkServiceMessages.HttpStatusError1, (int)response.StatusCode), false);
            }

            return await WaitForOtaCompletionAsync(client, token, statusChanged);
        }
        catch
        {
            return (false, WioLinkServiceMessages.ServerConnectionFailed, false);
        }
    }

    private static async Task<(bool Success, string? ErrorMessage, bool Unauthorized)> WaitForOtaCompletionAsync(
        HttpClient client,
        string token,
        Func<string, Task>? statusChanged)
    {
        var deadline = DateTimeOffset.UtcNow.AddMinutes(3);
        string? lastStatus = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            var response = await client.GetAsync($"v1/ota/status?access_token={token}");

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return (false, WioLinkServiceMessages.AuthenticationFailed, true);
            }

            if (!response.IsSuccessStatusCode)
            {
                return (false, string.Format(WioLinkServiceMessages.HttpStatusError1, (int)response.StatusCode), false);
            }

            var content = await response.Content.ReadAsStringAsync();
            var status = ExtractOtaStatus(content);
            var message = ExtractOtaMessage(content);
            lastStatus = string.IsNullOrWhiteSpace(message) ? status : message;

            if (statusChanged is not null && !string.IsNullOrWhiteSpace(lastStatus))
            {
                await statusChanged(lastStatus);
            }

            if (IsOtaCompletedStatus(status))
            {
                return (true, null, false);
            }

            if (IsOtaFailedStatus(status))
            {
                return (false, string.IsNullOrWhiteSpace(lastStatus) ? "OTA更新でエラーが発生しました。" : $"OTA更新でエラーが発生しました。状態: {lastStatus}", false);
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        return (false, string.IsNullOrWhiteSpace(lastStatus) ? "OTA更新の完了を確認できませんでした。" : $"OTA更新の完了待機がタイムアウトしました。最後の状態: {lastStatus}", false);
    }

    private static string ExtractOtaStatus(string content)
    {
        return ExtractOtaField(content, "ota_status");
    }

    private static string ExtractOtaMessage(string content)
    {
        return ExtractOtaField(content, "ota_msg");
    }

    private static string ExtractOtaField(string content, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            if (TryGetOtaField(document.RootElement, fieldName, out var value))
            {
                return value;
            }
        }
        catch (JsonException)
        {
        }

        return content.Trim();
    }

    private static bool TryGetOtaField(JsonElement element, string fieldName, out string value)
    {
        value = string.Empty;

        if (element.ValueKind != JsonValueKind.Object)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                value = element.GetString() ?? string.Empty;
                return !string.IsNullOrWhiteSpace(value);
            }

            return false;
        }

        if (element.TryGetProperty(fieldName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

        foreach (var nestedProperty in element.EnumerateObject())
        {
            if (nestedProperty.Value.ValueKind == JsonValueKind.Object && TryGetOtaField(nestedProperty.Value, fieldName, out value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsOtaCompletedStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        return status.Contains("done", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOtaFailedStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        return status.Contains("error", StringComparison.OrdinalIgnoreCase);
    }

    private static List<OtaConnectionItem> ExtractNodeConfigConnections(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            var connections = new List<OtaConnectionItem>();
            CollectNodeConfigConnections(document.RootElement, connections);
            return connections;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static void CollectNodeConfigConnections(JsonElement element, List<OtaConnectionItem> connections)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                CollectNodeConfigConnections(item, connections);
            }

            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (TryGetConnectionItem(element, out var connection))
        {
            connections.Add(connection);
            return;
        }

        if (element.TryGetProperty("connections", out var nestedConnections))
        {
            CollectNodeConfigConnections(nestedConnections, connections);
        }

        foreach (var nestedProperty in element.EnumerateObject())
        {
            if (string.Equals(nestedProperty.Name, "connections", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (nestedProperty.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                CollectNodeConfigConnections(nestedProperty.Value, connections);
            }
        }
    }

    private static bool TryGetConnectionItem(JsonElement element, out OtaConnectionItem connection)
    {
        connection = default!;

        if (!TryReadJsonString(element, "port", out var port) || !TryReadJsonString(element, "sku", out var sku))
        {
            return false;
        }

        connection = new OtaConnectionItem(port, sku);
        return true;
    }

    private static bool TryReadJsonString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;

        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private HttpClient CreateWioServerClient()
    {
        var client = httpClientFactory.CreateClient("WioServerApi");
        client.BaseAddress = new Uri(ServerBaseAddress);
        return client;
    }

    private static string NormalizeServerBaseAddress(string serverBaseUrl)
    {
        var baseUrl = serverBaseUrl.Trim();
        if (!baseUrl.EndsWith("/", StringComparison.Ordinal))
        {
            baseUrl += "/";
        }

        return baseUrl;
    }

    private static string ResolveGroveImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return string.Empty;
        }

        if (imageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return imageUrl;
        }

        var trimmed = imageUrl.StartsWith("/", StringComparison.Ordinal) ? imageUrl[1..] : imageUrl;
        return $"{GroveImagePrefix}{trimmed}";
    }

    private class LoginResponse
    {
        [JsonPropertyName("token")]
        public string? Token { get; set; }
    }

    private class NodesListResponse
    {
        [JsonPropertyName("nodes")]
        public List<NodeItem>? Nodes { get; set; }
    }

    private class GroveDriverListResponse
    {
        [JsonPropertyName("drivers")]
        public List<GroveDriverItem>? Drivers { get; set; }
    }

    public class NodeItem
    {
        [JsonPropertyName("node_key")]
        public string NodeKey { get; set; } = string.Empty;

        [JsonPropertyName("node_sn")]
        public string NodeSn { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("board")]
        public string Board { get; set; } = string.Empty;

        [JsonPropertyName("online")]
        public bool Online { get; set; }
    }

    public class GroveDriverItem
    {
        [JsonPropertyName("GroveName")]
        public string GroveName { get; set; } = string.Empty;

        [JsonPropertyName("SKU")]
        public string Sku { get; set; } = string.Empty;

        [JsonPropertyName("ImageURL")]
        public string ImageUrl { get; set; } = string.Empty;

        [JsonPropertyName("InterfaceType")]
        public string InterfaceType { get; set; } = string.Empty;
    }

    public sealed record OtaConnectionItem(
        [property: JsonPropertyName("port")] string Port,
        [property: JsonPropertyName("sku")] string Sku);

    private sealed class OtaTriggerRequest
    {
        [JsonPropertyName("board_name")]
        public string BoardName { get; set; } = string.Empty;

        [JsonPropertyName("connections")]
        public List<OtaConnectionItem> Connections { get; set; } = [];
    }
}
