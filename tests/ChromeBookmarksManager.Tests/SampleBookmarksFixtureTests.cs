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
    public void Fixture_ContainsV02MetadataAndUnknownFieldShapes()
    {
        Assert.True(File.Exists(FixturePath), $"Fixture not found: {FixturePath}");

        using var document = JsonDocument.Parse(File.ReadAllText(FixturePath));
        var root = document.RootElement;
        var roots = root.GetProperty("roots");
        var documentation = roots
            .GetProperty("bookmark_bar")
            .GetProperty("children")[0]
            .GetProperty("children")[0];
        var network = roots
            .GetProperty("other")
            .GetProperty("children")[0];

        Assert.Equal("synthetic-not-a-chrome-sha256", root.GetProperty("checksum_sha256").GetString());
        Assert.Equal(JsonValueKind.Object, root.GetProperty("future_document").ValueKind);
        Assert.Equal(JsonValueKind.Object, roots.GetProperty("future_root").ValueKind);
        Assert.Equal("13370000000000001", documentation.GetProperty("date_last_used").GetString());
        Assert.Equal(JsonValueKind.Object, documentation.GetProperty("future_node").ValueKind);
        Assert.Equal(
            JsonValueKind.Object,
            network.GetProperty("meta_info").GetProperty("nested").ValueKind);
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
