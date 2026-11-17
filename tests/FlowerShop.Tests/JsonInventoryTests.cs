using FlowerShop;
using Xunit.Sdk;

namespace FlowerShop.Tests;

public class JsonInventoryTests
{
    private const string ValidJson = "[{\"flower\":\"Rose\",\"stock\":100},{\"flower\":\"Lily\",\"stock\":8}]";
    private const string MalformedJson = "[{\"flower\":\"Rose\",\"stock\":100},{\"flower\":\"Lily\",\"stock\":}]";

    private static T ValueOf<T>(Result<T, AppError> result) =>
        result.Match(
            value => value,
            error => throw new XunitException($"Expected success but got [{error.Code}] {error.Message}"));

    private static AppError ErrorOf<T>(Result<T, AppError> result) =>
        result.Match<AppError>(
            _ => throw new XunitException("Expected a failure but got a success."),
            error => error);

    private sealed class TempDirectory : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "FlowerShopJson_" + Guid.NewGuid().ToString("N"));

        public TempDirectory() { Directory.CreateDirectory(Root); }

        public string PathFor(string name) => Path.Combine(Root, name);

        public string Write(string name, string content)
        {
            var path = PathFor(name);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    [Fact]
    public void ValidJson_LoadsEveryItem_IncludingZeroStock()
    {
        var json = "[{\"flower\":\"Rose\",\"stock\":5},{\"flower\":\"Sunflower\",\"stock\":0}]";

        var inventory = ValueOf(InventoryJsonLoader.Parse(json, "test.json"));

        Assert.Equal(2, inventory.Count);
        Assert.Equal(0, inventory.Find(FlowerType.Sunflower).Match(item => item.Stock, () => -1));
    }

    [Fact]
    public void MalformedJson_IsAClearOperationalFailure()
    {
        var error = ErrorOf(InventoryJsonLoader.Parse(MalformedJson, "broken.json"));

        Assert.Equal(ErrorKind.Operational, error.Kind);
        Assert.Equal("malformed_json", error.Code);
        Assert.Contains("broken.json", error.Message);
        Assert.Contains("invalid JSON", error.Message);
    }

    [Fact]
    public void MalformedJson_PreviouslyLoadedValidData_RemainsUnchanged()
    {
        using var temp = new TempDirectory();
        var goodPath = temp.Write("good.json", ValidJson);
        var badPath = temp.Write("bad.json", MalformedJson);
        var loadedBefore = ValueOf(InventoryJsonLoader.Load(goodPath));

        var failed = InventoryJsonLoader.Load(badPath);

        Assert.True(failed.IsFailure);
        Assert.Equal("malformed_json", ErrorOf(failed).Code);
        Assert.Equal(2, loadedBefore.Count);
        Assert.Equal(100, loadedBefore.Find(FlowerType.Rose).Match(item => item.Stock, () => -1));
        Assert.Equal(8, loadedBefore.Find(FlowerType.Lily).Match(item => item.Stock, () => -1));
        Assert.Equal(2, ValueOf(InventoryJsonLoader.Load(goodPath)).Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("[")]
    [InlineData("not json")]
    [InlineData("[{\"flower\":\"Rose\",}]")]
    public void BrokenJsonSyntax_IsAlwaysMalformedJson(string json)
    {
        var result = InventoryJsonLoader.Parse(json, "test.json");

        Assert.Equal("malformed_json", ErrorOf(result).Code);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[1]")]
    [InlineData("[{\"flower\":\"Rose\"}]")]
    [InlineData("[{\"flower\":\"Rose\",\"stock\":\"5\"}]")]
    [InlineData("[{\"flower\":\"Rose\",\"stock\":-1}]")]
    [InlineData("[{\"flower\":\"Rose\",\"stock\":1.5}]")]
    [InlineData("[{\"flower\":\"Orchid\",\"stock\":5}]")]
    [InlineData("[{\"flower\":\"Rose\",\"stock\":5},{\"flower\":\"Rose\",\"stock\":6}]")]
    [InlineData("[{\"flower\":7,\"stock\":5}]")]
    public void ValidJsonWithWrongContent_IsMalformedContent(string json)
    {
        var result = InventoryJsonLoader.Parse(json, "test.json");

        Assert.Equal("malformed_content", ErrorOf(result).Code);
    }

    [Fact]
    public void MissingJsonFile_IsReportedAsFileNotFound()
    {
        using var temp = new TempDirectory();
        var missing = temp.PathFor("nope.json");

        var error = ErrorOf(InventoryJsonLoader.Load(missing));

        Assert.Equal("file_not_found", error.Code);
        Assert.Contains(missing, error.Message);
    }

    [Fact]
    public void JsonInventory_FlowsThroughTheQuoteWorkflow()
    {
        var inventory = ValueOf(InventoryJsonLoader.Parse(ValidJson, "test.json"));
        var order = new Order("Q1", "Aigerim", FlowerType.Rose, 10, 5, false);

        var display = ValueOf(QuoteWorkflow.RequestQuote(order, inventory, ShopRules.Default, DeliveryPolicies.Standard));

        Assert.Equal(2550m, display.Total);
    }
}
