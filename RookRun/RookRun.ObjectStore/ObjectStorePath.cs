namespace RookRun.ObjectStore;

internal static class ObjectStorePath
{
    public static string NormalizeRequiredPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var normalized = path.Replace('\\', '/').Trim('/');
        if (normalized.Length == 0)
        {
            throw new ArgumentException("Path must not be empty.", nameof(path));
        }

        ValidateSegments(normalized, nameof(path));
        return normalized;
    }

    public static string NormalizePrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return string.Empty;
        }

        var normalized = prefix.Replace('\\', '/').Trim('/');
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        ValidateSegments(normalized, nameof(prefix));
        return normalized;
    }

    private static void ValidateSegments(string value, string paramName)
    {
        foreach (var segment in value.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment is "." or "..")
            {
                throw new ArgumentException("Path segments cannot be '.' or '..'.", paramName);
            }
        }
    }
}