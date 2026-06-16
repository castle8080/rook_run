using RookRun.ObjectStore;
using System.IO;
using System.Text.Json;

namespace RookRun.Strava.Client.Auth;

/// <summary>
/// Persists Strava token data in the configured object store.
/// </summary>
public sealed class ObjectStoreStravaTokenStore : IStravaTokenStore
{
    private const string TokenPath = "secrets/strava/auth_token.json.br";
    private readonly IObjectStore objectStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectStoreStravaTokenStore"/> class.
    /// </summary>
    /// <param name="objectStore">The object store used for token persistence.</param>
    public ObjectStoreStravaTokenStore(IObjectStore objectStore)
    {
        this.objectStore = objectStore ?? throw new ArgumentNullException(nameof(objectStore));
    }

    /// <inheritdoc />
    public async Task<StravaStoredToken?> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var storedToken = await objectStore.TryReadObjectAsync<StravaStoredToken>(TokenPath, cancellationToken: cancellationToken);
            if (!storedToken.IsFound)
            {
                return null;
            }

            if (storedToken.Value is null)
            {
                await objectStore.TryDeleteObjectAsync(TokenPath, cancellationToken);
                return null;
            }

            return storedToken.Value;
        }
        catch (JsonException)
        {
            await objectStore.TryDeleteObjectAsync(TokenPath, cancellationToken);
            return null;
        }
        catch (InvalidDataException)
        {
            await objectStore.TryDeleteObjectAsync(TokenPath, cancellationToken);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(StravaStoredToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);
        await objectStore.StoreObjectAsync(TokenPath, token, overwrite: true, cancellationToken);
    }
}