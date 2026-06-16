using Microsoft.Extensions.Options;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RookRun.Strava.Client.Auth;

/// <summary>
/// Persists Strava token data to a DPAPI-protected file for the current Windows user.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsDpapiStravaTokenStore : IStravaTokenStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly string _filePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsDpapiStravaTokenStore"/> class.
    /// </summary>
    /// <param name="options">The configured token store options.</param>
    public WindowsDpapiStravaTokenStore(IOptions<StravaTokenStoreOptions> options)
    {
        var value = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _filePath = ResolveFilePath(value);
    }

    /// <inheritdoc />
    public async Task<StravaStoredToken?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        try
        {
            var protectedBytes = await File.ReadAllBytesAsync(_filePath, cancellationToken);
            var jsonBytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<StravaStoredToken>(jsonBytes, SerializerOptions);
        }
        catch (CryptographicException)
        {
            DeleteIfExists();
            return null;
        }
        catch (JsonException)
        {
            DeleteIfExists();
            return null;
        }
        catch (IOException)
        {
            DeleteIfExists();
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(StravaStoredToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(token, SerializerOptions);
        var protectedBytes = ProtectedData.Protect(jsonBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        await File.WriteAllBytesAsync(_filePath, protectedBytes, cancellationToken);
    }

    private static string ResolveFilePath(StravaTokenStoreOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.FilePath))
        {
            return options.FilePath;
        }

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appDataPath, "RookRun", "strava-token.dat");
    }

    private void DeleteIfExists()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
