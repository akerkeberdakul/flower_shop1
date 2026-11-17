namespace FlowerShop;

public static class LineStream
{
    public static IEnumerable<string> Read(Func<TextReader> open)
    {
        ArgumentNullException.ThrowIfNull(open);

        return Iterate(open);
    }

    public static IEnumerable<string> ReadFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return Iterate(() => new StreamReader(path));
    }

    private static IEnumerable<string> Iterate(Func<TextReader> open)
    {
        using var reader = open();

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            yield return line;
        }
    }
}
