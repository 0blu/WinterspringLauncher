using System;
using System.Collections.Generic;
using System.Data;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinterspringLauncher.Utils;

public static class GitHubApi
{
    public static GitHubReleaseInfo LatestReleaseVersion(string repoName)
    {
        var releaseUrl = $"https://api.github.com/repos/{repoName}/releases/latest";
        var releaseInfo = PerformWebRequest<GitHubReleaseInfo>(releaseUrl);
        return releaseInfo;
    }

    private static TJsonResponse PerformWebRequest<TJsonResponse>(string url) where TJsonResponse : new()
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "curl/7.0.0"); // otherwise we get blocked
        var response = client.GetAsync(url).GetAwaiter().GetResult();
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            if (response.ReasonPhrase == "rate limit exceeded")
            {
                Console.WriteLine("You are being rate-limited, did you open the launcher too many times in a short time?");
                return new TJsonResponse();
            }
        }
        response.EnsureSuccessStatusCode();
        var rawJson = response.Content.ReadAsStringAsync().GetAwaiter().GetResult(); // easier to debug with a string and the performance is negligible for such small jsons
        var parsedJson = JsonSerializer.Deserialize<TJsonResponse>(rawJson);
        if (parsedJson == null)
        {
            Console.WriteLine($"Debug: {rawJson}");
            throw new NoNullAllowedException("The web response resulted in an null object");
        }
        return parsedJson;
    }
}

public class GitHubReleaseInfo
{
    [JsonPropertyName("name")] 
    public string? Name { get; set; }
    
    [JsonPropertyName("tag_name")] 
    public string? TagName { get; set; }

    [JsonPropertyName("assets")] 
    public List<Asset>? Assets { get; set; }

    public class Asset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = null!;

        [JsonPropertyName("browser_download_url")]
        public string DownloadUrl { get; set; } = null!;
    }
}
