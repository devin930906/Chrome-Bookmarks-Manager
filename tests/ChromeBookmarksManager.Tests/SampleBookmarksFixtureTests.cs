using System.Text.Json;

namespace ChromeBookmarksManager.Tests;

public sealed class SampleBookmarksFixtureTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Bookmarks.sample.json");

    [Fact]
    public void Fixture_IsValidJsonAndUsesVersionOne()
    {
        Assert.True(File.Exists(FixturePath), $"Fixture not found: {FixturePath}");

        using var document = JsonDocument.Parse(File.ReadAllText(FixturePath));

        Assert.Equal(1, document.RootElement.GetProperty("version").GetInt32());
        Assert.True(document.RootElement.GetProperty("roots").TryGetProperty("bookmark_bar", out _));
        Assert.True(document.RootElement.GetProperty("roots").TryGetProperty("other", out _));
        Assert.True(document.RootElement.GetProperty("roots").TryGetProperty("synced", out _));
    }

    [Fact]
    public void Fixture_ContainsOnlyReservedExampleHosts()
    {
        Assert.True(File.Exists(FixturePath), $"Fixture not found: {FixturePath}");

        using var document = JsonDocument.Parse(File.ReadAllText(FixturePath));
        var hosts = new List<string>();

        CollectHosts(document.RootElement, hosts);

        Assert.NotEmpty(hosts);
        Assert.All(hosts, host =>
            Assert.Contains(host, new[] { "example.com", "www.example.com", "example.net", "example.org" }));
    }

    private static void CollectHosts(JsonElement element, ICollection<string> hosts)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("url", out var urlElement)
                && Uri.TryCreate(urlElement.GetString(), UriKind.Absolute, out var uri))
            {
                hosts.Add(uri.Host);
            }

            foreach (var property in element.EnumerateObject())
            {
                CollectHosts(property.Value, hosts);
            }

            return;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                CollectHosts(child, hosts);
            }
        }
    }
}
