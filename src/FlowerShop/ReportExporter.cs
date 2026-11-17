using System.Text;

namespace FlowerShop;

public static class ReportExporter
{
    public static Result<string, AppError> Write(Func<Stream> open, IEnumerable<string> lines, string target)
    {
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(target);

        try
        {
            using var stream = open();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));

            foreach (var line in lines)
            {
                writer.WriteLine(line);
            }

            writer.Flush();
            return Result<string, AppError>.Ok(target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Failure(target, ex.Message);
        }
    }

    public static Result<string, AppError> ExportToFile(string? path, IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        if (string.IsNullOrWhiteSpace(path))
        {
            return Result<string, AppError>.Fail(AppError.Operational("invalid_path", "No path was given for the report file."));
        }

        var temporary = path + ".tmp";
        var published = false;

        try
        {
            var result = Write(() => new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None), lines, path)
                .Bind(_ => Publish(temporary, path));

            published = result.IsSuccess;
            return result;
        }
        finally
        {
            if (!published)
            {
                TryDelete(temporary);
            }
        }
    }

    private static Result<string, AppError> Publish(string temporary, string target)
    {
        try
        {
            File.Move(temporary, target, overwrite: true);
            return Result<string, AppError>.Ok(target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Failure(target, ex.Message);
        }
    }

    private static bool TryDelete(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static Result<string, AppError> Failure(string target, string details) =>
        Result<string, AppError>.Fail(AppError.Operational(
            "export_failed",
            $"The report could not be written to '{target}': {details}"));
}
