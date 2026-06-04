using Microsoft.Playwright;
using RookRun.Garmin.Options;

namespace RookRun.Garmin;

public sealed class GarminActivities : IGarminActivities, IAsyncDisposable
{
    private readonly string _baseUrl;
    private readonly string _browserName;
    private readonly string? _profilePath;
    private readonly string _username;
    private readonly string _password;
    private readonly string _userAgent;
    private readonly bool _headless;

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _browserContext;
    private IPage? _page;

    public GarminActivities(GarminOptions options)
        : this(options.BaseUrl, options.Browser, options.ProfilePath, options.Username, options.Password, options.UserAgent, options.Headless)
    {
    }

    public GarminActivities(string baseUrl, string browserName, string? profilePath, string username, string password, string userAgent, bool headless = true)
    {
        _baseUrl = baseUrl;
        _browserName = browserName;
        _profilePath = string.IsNullOrWhiteSpace(profilePath) ? null : profilePath;
        _username = username;
        _password = password;
        _userAgent = userAgent;
        _headless = headless;
    }

    private async Task<IPage> GetPageAsync()
    {
        if (_page is not null)
        {
            return _page;
        }

        _playwright ??= await Playwright.CreateAsync();

        if (_browserContext is null)
        {
            if (_profilePath is not null)
            {
                Directory.CreateDirectory(_profilePath);
                _browserContext = await GetBrowserType(_playwright).LaunchPersistentContextAsync(_profilePath, new BrowserTypeLaunchPersistentContextOptions
                {
                    Headless = _headless,
                    UserAgent = _userAgent
                });
            }
            else
            {
                _browser ??= await GetBrowserType(_playwright).LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = _headless
                });
                _browserContext = await _browser.NewContextAsync(new BrowserNewContextOptions
                {
                    UserAgent = _userAgent
                });
            }
        }

        _page = _browserContext.Pages.FirstOrDefault() ?? await _browserContext.NewPageAsync();
        return _page!;
    }

    public async Task LoginAsync()
    {
        var page = await GetPageAsync();
        await page.GotoAsync(BuildSignInUri().ToString(), new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });

        await FillFirstAsync(
            page,
            _username,
            "input[name='username']",
            "input[type='email']",
            "input[id='email']");

        await FillFirstAsync(
            page,
            _password,
            "input[name='password']",
            "input[type='password']");

        await ClickFirstAsync(
            page,
            "button[type='submit']",
            "input[type='submit']",
            "button:has-text('Sign In')",
            "button:has-text('Log In')");

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async ValueTask DisposeAsync()
    {
        if (_page is not null)
        {
            await _page.CloseAsync();
            _page = null;
        }

        if (_browserContext is not null)
        {
            await _browserContext.DisposeAsync();
            _browserContext = null;
        }

        if (_browser is not null)
        {
            await _browser.DisposeAsync();
            _browser = null;
        }

        _playwright?.Dispose();
        _playwright = null;
    }

    private Uri BuildSignInUri() => new(new Uri($"{_baseUrl.TrimEnd('/')}/"), "signin/");

    private IBrowserType GetBrowserType(IPlaywright playwright) => _browserName.Trim().ToLowerInvariant() switch
    {
        "chromium" => playwright.Chromium,
        "firefox" => playwright.Firefox,
        "webkit" => playwright.Webkit,
        _ => throw new InvalidOperationException($"Unsupported Playwright browser '{_browserName}'. Supported values are: chromium, firefox, webkit.")
    };

    private static async Task FillFirstAsync(IPage page, string value, params string[] selectors)
    {
        foreach (var selector in selectors)
        {
            var locator = page.Locator(selector).First;
            if (await locator.CountAsync() == 0)
            {
                continue;
            }

            await locator.FillAsync(value);
            return;
        }

        throw new InvalidOperationException($"Could not find any matching input using selectors: {string.Join(", ", selectors)}");
    }

    private static async Task ClickFirstAsync(IPage page, params string[] selectors)
    {
        foreach (var selector in selectors)
        {
            var locator = page.Locator(selector).First;
            if (await locator.CountAsync() == 0)
            {
                continue;
            }

            await locator.ClickAsync();
            return;
        }

        throw new InvalidOperationException($"Could not find any matching button using selectors: {string.Join(", ", selectors)}");
    }
}
