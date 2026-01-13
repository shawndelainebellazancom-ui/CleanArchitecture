using Microsoft.Playwright;

namespace ProjectName.McpServer.Services;

/// <summary>
/// Manages Playwright browser lifecycle and page instances
/// Implements singleton pattern for browser reuse across tool calls
/// </summary>
public class PlaywrightManager : IAsyncDisposable
{
    private readonly ILogger<PlaywrightManager> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _currentPage;
    private bool _isInitialized;

    public PlaywrightManager(ILogger<PlaywrightManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Initialize Playwright and launch browser
    /// Thread-safe, idempotent
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        await _initLock.WaitAsync();
        try
        {
            if (_isInitialized)
                return;

            _logger.LogInformation("Initializing Playwright...");

            // Install Playwright browsers if not already installed
            var exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
            if (exitCode != 0)
            {
                _logger.LogWarning("Playwright install returned exit code: {ExitCode}", exitCode);
            }

            _playwright = await Playwright.CreateAsync();

            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = new[]
                {
                    "--disable-blink-features=AutomationControlled",
                    "--disable-dev-shm-usage",
                    "--no-sandbox"
                }
            });

            _logger.LogInformation("Playwright browser launched successfully");
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Playwright");
            throw;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Get or create the current page instance
    /// </summary>
    public async Task<IPage> GetPageAsync()
    {
        if (!_isInitialized)
        {
            await InitializeAsync();
        }

        if (_currentPage == null || _currentPage.IsClosed)
        {
            if (_browser == null)
            {
                throw new InvalidOperationException("Browser not initialized");
            }

            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"
            });

            _currentPage = await context.NewPageAsync();

            _logger.LogInformation("New browser page created");
        }

        return _currentPage;
    }

    /// <summary>
    /// Close current page and create new one
    /// </summary>
    public async Task ResetPageAsync()
    {
        if (_currentPage != null && !_currentPage.IsClosed)
        {
            await _currentPage.CloseAsync();
            _currentPage = null;
        }

        _logger.LogInformation("Browser page reset");
    }

    /// <summary>
    /// Get browser instance (for advanced scenarios)
    /// </summary>
    public IBrowser? Browser => _browser;

    public async ValueTask DisposeAsync()
    {
        _logger.LogInformation("Disposing Playwright resources...");

        if (_currentPage != null && !_currentPage.IsClosed)
        {
            await _currentPage.CloseAsync();
        }

        if (_browser != null)
        {
            await _browser.CloseAsync();
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();
        _initLock.Dispose();

        _logger.LogInformation("Playwright resources disposed");
    }
}