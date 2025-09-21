using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace NeighborHood.Services;

public enum WorkerState
{
    Idle,
    Busy,
    Error,
    Disposed
}

public class SearchWorker : IDisposable
{
    private IWebDriver? _driver;
    private WebDriverWait? _wait;
    private readonly Random _random = new();
    private bool _disposed;
    private WorkerState _state;
    private readonly string _chromeDriverPath;
    private readonly int _initTimeoutSeconds;
    private readonly int _retryAttempts;

    public int WorkerId { get; }
    public WorkerState State
    {
        get => _state;
        private set
        {
            if (_state != value)
            {
                _state = value;
                StateChanged?.Invoke(this, new StateChangedEventArgs(WorkerId, value));
            }
        }
    }

    public event EventHandler<StateChangedEventArgs>? StateChanged;

    public class StateChangedEventArgs : EventArgs
    {
        public int WorkerId { get; }
        public WorkerState NewState { get; }

        public StateChangedEventArgs(int workerId, WorkerState newState)
        {
            WorkerId = workerId;
            NewState = newState;
        }
    }

    private string GetChromeVersion()
    {
        try
        {
            var chromePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
            if (!File.Exists(chromePath))
            {
                chromePath = @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe";
            }

            if (File.Exists(chromePath))
            {
                var version = System.Diagnostics.FileVersionInfo.GetVersionInfo(chromePath);
                return version.FileVersion ?? "Unknown";
            }

            return "Chrome not found";
        }
        catch (Exception ex)
        {
            return $"Error getting Chrome version: {ex.Message}";
        }
    }

    public SearchWorker(int workerId, IConfiguration configuration)
    {
        WorkerId = workerId;
        State = WorkerState.Idle;
        
        _chromeDriverPath = configuration.GetValue<string>("WebDriver:ChromeDriverPath") ?? "runtimes\\win\\native";
        _initTimeoutSeconds = configuration.GetValue<int>("WebDriver:TimeoutSeconds", 30);
        _retryAttempts = configuration.GetValue<int>("WebDriver:RetryAttempts", 3);
        
        InitializeDriver();
    }

    private void InitializeDriver()
    {
        try
        {
            var options = new ChromeOptions();
            
            // 필수 기본 옵션
            options.AddArgument("--headless");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            
            // Chrome 경로 설정
            var possiblePaths = new[]
            {
                @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"
            };
            
            var chromePath = possiblePaths.FirstOrDefault(File.Exists);
            if (chromePath != null)
            {
                options.BinaryLocation = chromePath;
                Console.WriteLine($"[Worker {WorkerId}] Found Chrome at: {chromePath}");
            }
            else
            {
                Console.WriteLine($"[Worker {WorkerId}] Warning: Chrome not found in standard locations");
            }
            
            // ChromeDriver 서비스 설정
            var outputDir = Path.GetDirectoryName(typeof(SearchWorker).Assembly.Location)
                ?? throw new InvalidOperationException("Could not determine assembly directory");
            Console.WriteLine($"[Worker {WorkerId}] Assembly directory: {outputDir}");
            
            var service = ChromeDriverService.CreateDefaultService(outputDir);
            service.HideCommandPromptWindow = true;
            service.LogPath = Path.Combine(outputDir, $"chromedriver_{WorkerId}.log");
            service.EnableVerboseLogging = true;
            
            var chromeDriverPath = Path.Combine(outputDir, "chromedriver.exe");
            if (File.Exists(chromeDriverPath))
            {
                Console.WriteLine($"[Worker {WorkerId}] Found ChromeDriver at: {chromeDriverPath}");
            }
            else
            {
                throw new FileNotFoundException($"ChromeDriver not found at: {chromeDriverPath}");
            }
            
            Console.WriteLine($"[Worker {WorkerId}] Initializing ChromeDriver...");
            _driver = new ChromeDriver(service, options);
            _driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(30);
            _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
            
            _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(30));
            _wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException), typeof(NoSuchElementException));
            
            Console.WriteLine($"[Worker {WorkerId}] ChromeDriver initialized successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Worker {WorkerId}] Failed to initialize ChromeDriver: {ex.GetType().Name} - {ex.Message}");
            Console.WriteLine($"[Worker {WorkerId}] Stack trace: {ex.StackTrace}");
            _driver?.Quit();
            _driver?.Dispose();
            throw;
        }
    }

    // This method has been merged with InitializeDriver
    private void OldInitializeDriverWithRetry()
    {
        var driverPath = AppDomain.CurrentDomain.BaseDirectory;
        var chromeDriverPath = Path.Combine(driverPath, "chromedriver.exe");

        Console.WriteLine($"[Worker {WorkerId}] Looking for ChromeDriver at: {chromeDriverPath}");

        if (!File.Exists(chromeDriverPath))
        {
            throw new FileNotFoundException($"ChromeDriver not found at {chromeDriverPath}");
        }

        var options = new ChromeOptions();
        options.AddArgument("--no-sandbox");
        options.AddArgument("--headless");
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument("--disable-gpu");
        options.AddArgument("--log-level=3");
        
        // Chrome 바이너리 위치 명시적 설정
        var chromePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
        if (!File.Exists(chromePath))
        {
            chromePath = @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe";
        }
        if (File.Exists(chromePath))
        {
            options.BinaryLocation = chromePath;
            Console.WriteLine($"[Worker {WorkerId}] Using Chrome binary at: {chromePath}");
        }
        
        // 자동화 감지 방지
        options.AddExcludedArgument("enable-automation");
        options.AddAdditionalOption("useAutomationExtension", false);
        options.AddUserProfilePreference("profile.default_content_setting_values.notifications", 2);

        var driverService = ChromeDriverService.CreateDefaultService(driverPath);
        driverService.HideCommandPromptWindow = true;
        driverService.SuppressInitialDiagnosticInformation = true;
        driverService.LogPath = "chromedriver.log";
        driverService.EnableVerboseLogging = true;
        
        Console.WriteLine($"[Worker {WorkerId}] ChromeDriver service path: {driverService.DriverServicePath}");
        Console.WriteLine($"[Worker {WorkerId}] ChromeDriver executable path: {Path.Combine(driverPath, "chromedriver.exe")}");
        
        Console.WriteLine($"[Worker {WorkerId}] Initializing ChromeDriver using path: {driverPath}");

        try
        {
            try
            {
                Console.WriteLine($"[Worker {WorkerId}] Chrome version check...");
                var chromeVersion = GetChromeVersion();
                Console.WriteLine($"[Worker {WorkerId}] Detected Chrome version: {chromeVersion}");

                var initTask = Task.Run(() =>
                {
                    try
                    {
                        Console.WriteLine($"[Worker {WorkerId}] Starting ChromeDriver initialization...");
                        _driver = new ChromeDriver(driverService, options);
                        Console.WriteLine($"[Worker {WorkerId}] ChromeDriver instance created successfully");
                        
                        _driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(20);
                        _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
                        return _driver;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Worker {WorkerId}] Error in ChromeDriver initialization thread: {ex.Message}");
                        throw;
                    }
                });

                Console.WriteLine($"[Worker {WorkerId}] Waiting for ChromeDriver initialization...");
                if (!initTask.Wait(TimeSpan.FromSeconds(30)))
                {
                    throw new TimeoutException("ChromeDriver initialization timed out after 30 seconds");
                }
                Console.WriteLine($"[Worker {WorkerId}] ChromeDriver initialization completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Worker {WorkerId}] Detailed initialization error: {ex.ToString()}");
                throw;
            }

            if (_driver == null)
            {
                throw new InvalidOperationException("ChromeDriver initialization failed");
            }

            _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(30));
            _wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException), typeof(NoSuchElementException));

            _driver.Manage().Window.Maximize();

            Console.WriteLine($"[Worker {WorkerId}] WebDriver initialized successfully");
        }
        catch (TimeoutException ex)
        {
            Console.WriteLine($"[Worker {WorkerId}] ⚠️ ChromeDriver initialization timed out: {ex.Message}");
            _driver?.Quit();
            _driver?.Dispose();
            throw;
        }
        catch (WebDriverException ex)
        {
            Console.WriteLine($"[Worker {WorkerId}] ⚠️ WebDriver error: {ex.Message}");
            _driver?.Quit();
            _driver?.Dispose();
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Worker {WorkerId}] ⚠️ Unexpected error during initialization: {ex.Message}");
            _driver?.Quit();
            _driver?.Dispose();
            throw;
        }
    }

    public async Task ExecuteSearchAsync(string keyword, string? targetText, string? targetUrl)
    {
        try
        {
            State = WorkerState.Busy;
            Console.WriteLine($"[Worker {WorkerId}] Starting search for: {keyword}");
            await PerformSearch(keyword, targetText, targetUrl);
            Console.WriteLine($"[Worker {WorkerId}] Search completed successfully");
            State = WorkerState.Idle;
        }
        catch (Exception ex)
        {
            State = WorkerState.Error;
            Console.WriteLine($"[Worker {WorkerId}] Error during search: {ex.Message}");
            throw;
        }
    }

    private async Task PerformSearch(string keyword, string? targetText, string? targetUrl)
    {
        if (_driver == null) throw new InvalidOperationException("Driver not initialized");

        _driver.Navigate().GoToUrl("https://www.naver.com");
        await Task.Delay(_random.Next(1000, 2000));

        var searchBox = await FindElement("input[name='query']");
        if (searchBox == null) throw new Exception("Search box not found");

        await EnterSearchKeyword(searchBox, keyword);
        await FindAndClickResult(targetText, targetUrl);
    }

    private async Task<IWebElement?> FindElement(string selector, int timeoutSeconds = 5)
    {
        if (_driver == null || _wait == null) return null;

        try
        {
            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(timeoutSeconds));
            var element = wait.Until(d =>
            {
                try
                {
                    var el = d.FindElement(By.CssSelector(selector));
                    return el.Displayed && el.Enabled ? el : null;
                }
                catch
                {
                    return null;
                }
            });

            if (element != null)
            {
                await Task.Delay(_random.Next(300, 800));
                return element;
            }
        }
        catch
        {
            // Ignore and return null
        }

        return null;
    }

    private async Task EnterSearchKeyword(IWebElement searchBox, string keyword)
    {
        searchBox.Clear();
        await Task.Delay(_random.Next(300, 500));
        searchBox.SendKeys(keyword);
        await Task.Delay(_random.Next(500, 800));
        searchBox.Submit();
        await Task.Delay(_random.Next(1000, 2000));
    }

    private async Task FindAndClickResult(string? targetText, string? targetUrl)
    {
        if (_driver == null) return;

        try
        {
            Console.WriteLine($"[Worker {WorkerId}] 검색 결과에서 링크 찾는 중...");
            await Task.Delay(_random.Next(2000, 3000));

            int maxPageAttempts = 10;
            int currentAttempt = 0;

            while (currentAttempt < maxPageAttempts)
            {
                currentAttempt++;
                Console.WriteLine($"[Worker {WorkerId}] 페이지 {currentAttempt} 확인 중...");

                var elements = _driver.FindElements(By.CssSelector("a[href*='map.naver.com']"))
                    .Where(e =>
                        e.Displayed &&
                        e.GetAttribute("href")?.Contains("1883331965") == true)
                    .ToList();

                Console.WriteLine($"[Worker {WorkerId}] 발견된 요소 수: {elements.Count}");

                if (elements.Count > 0)
                {
                    var targetElement = elements.First();
                    var href = targetElement.GetAttribute("href");
                    Console.WriteLine($"[Worker {WorkerId}] ✅ 대상 링크 발견: {href}");

                    ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView(true);", targetElement);
                    await Task.Delay(_random.Next(500, 1000));

                    try
                    {
                        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", targetElement);
                    }
                    catch
                    {
                        targetElement.Click();
                    }

                    Console.WriteLine($"[Worker {WorkerId}] ✅ 링크 클릭 성공");
                    await Task.Delay(_random.Next(5000, 8000));

                    var currentHandle = _driver.CurrentWindowHandle;
                    var handles = _driver.WindowHandles;

                    if (handles.Count > 1)
                    {
                        Console.WriteLine($"[Worker {WorkerId}] 새 창으로 이동");
                        foreach (var handle in handles)
                        {
                            if (handle != currentHandle)
                            {
                                _driver.SwitchTo().Window(handle);
                                break;
                            }
                        }

                        await Task.Delay(_random.Next(2000, 3000));

                        _driver.Close();
                        _driver.SwitchTo().Window(currentHandle);
                    }
                    else
                    {
                        await Task.Delay(_random.Next(2000, 3000));
                        _driver.Navigate().Back();
                    }

                    Console.WriteLine($"[Worker {WorkerId}] ⬅️ 이전 페이지로 복귀");
                    await Task.Delay(_random.Next(1000, 2000));

                    // 다음 페이지 버튼 찾기
                    var nextButton = await FindElement("a.cmm_pg_next[aria-disabled='false']");
                    if (nextButton == null)
                    {
                        Console.WriteLine($"[Worker {WorkerId}] ❌ 더 이상 다음 페이지가 없습니다");
                        break;
                    }

                    // 다음 페이지로 이동
                    Console.WriteLine($"[Worker {WorkerId}] ➡️ 다음 페이지로 이동");
                    nextButton.Click();
                    await Task.Delay(_random.Next(2000, 3000)); // 페이지 로딩 대기
                }

                await Task.Delay(_random.Next(1000, 2000));
                continue;
            }

            Console.WriteLine($"[Worker {WorkerId}] ❌ 대상 링크를 찾지 못했습니다");
        }
        catch (WebDriverException ex)
        {
            Console.WriteLine($"[Worker {WorkerId}] ❌ 브라우저 작업 중 오류 발생: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Worker {WorkerId}] ❌ 예상치 못한 오류 발생: {ex.Message}");
            throw;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            State = WorkerState.Disposed;
            _driver?.Quit();
            _driver?.Dispose();
            _disposed = true;
        }
    }
}
