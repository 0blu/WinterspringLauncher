using System;
using System.Data;
using System.Net.Http;
using System.Text.Json;

namespace WinterspringLauncher.Utils;

public static class SimpleFileDownloader
{
    public static string PerformGetStringRequest(string url)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "curl/7.0.0"); // otherwise we get blocked
        var response = client.GetAsync(url).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
        var rawData = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        return rawData;
    }

    public static TJsonResponse PerformGetJsonRequest<TJsonResponse>(string url)
    {
        var rawData = PerformGetStringRequest(url);
        var parsedJson = JsonSerializer.Deserialize<TJsonResponse>(rawData);
        if (parsedJson == null)
        {
            Console.WriteLine($"Debug: {rawData}");
            throw new NoNullAllowedException("The web response resulted in an null object");
        }
        return parsedJson;
    }

    public static byte[] PerformGetBytesRequest(string url)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "curl/7.0.0"); // otherwise we get blocked
        var response = client.GetAsync(url).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
        var rawData = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
        return rawData;
    }
}
