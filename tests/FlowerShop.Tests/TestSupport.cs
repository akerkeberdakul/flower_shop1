using Xunit.Sdk;

namespace FlowerShop.Tests;

public static class TestSupport
{
    public static T ValueOf<T>(Result<T, AppError> result) =>
        result.Match(
            value => value,
            error => throw new XunitException($"Expected success but got [{error.Code}] {error.Message}"));

    public static AppError ErrorOf<T>(Result<T, AppError> result) =>
        result.Match<AppError>(
            _ => throw new XunitException("Expected a failure but got a success."),
            error => error);
}

public sealed class TempDirectory : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "FlowerShopX_" + Guid.NewGuid().ToString("N"));

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
