using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System.Collections.ObjectModel;

namespace NeighborHood.Services;

public class NaverSearchService : IDisposable
{
    private IWebDriver? _driver;
    private WebDriverWait? _wait;
    private bool _disposed = false;
    private readonly Random _random = new();

    public bool IsDriverHealthy()
    {
        if (_driver == null || _wait == null)
            return false;

        try
        {
            // 간단한 JavaScript 실행으로 브라우저 응답 확인
            ((IJavaScriptExecutor)_driver).ExecuteScript("return navigator.userAgent");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Driver health check failed: {ex.Message}");
            return false;
        }
    }

    public NaverSearchService()
    {
        InitializeDriver();
    }

    private void InitializeDriver()
    {
        Console.WriteLine("Starting Selenium WebDriver initialization...");
        var options = new ChromeOptions();
        Console.WriteLine("Configuring Chrome options...");
        options.AddArgument("--start-maximized");
        options.AddArgument("--headless=new");
        options.AddArgument("--disable-gpu");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument("--disable-web-security");
        options.AddArgument("--disable-blink-features=AutomationControlled");
        options.AddArgument("--log-level=3");
        options.AddArgument("--silent");
        options.AddArgument("--disable-logging");
        options.AddArgument("--remote-debugging-port=0");
        options.AddArgument("--ignore-certificate-errors");
        options.AddUserProfilePreference("profile.default_content_setting_values.notifications", 2);
        
        var service = ChromeDriverService.CreateDefaultService();
        service.HideCommandPromptWindow = true;
        service.SuppressInitialDiagnosticInformation = true;
        
        try
        {
            _driver?.Quit();
            _driver?.Dispose();
            
            Console.WriteLine("Creating new ChromeDriver instance...");
            _driver = new ChromeDriver(service, options);
            Console.WriteLine($"ChromeDriver created successfully. Browser version: {((ChromeDriver)_driver).Capabilities["browserVersion"]}");
            _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(30));  // 60초에서 30초로 감소
            _wait.IgnoreExceptionTypes(
                typeof(StaleElementReferenceException),
                typeof(NoSuchElementException)
            );
            
            _driver.Manage().Window.Maximize();
            _driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(20);  // 30초에서 20초로 감소
            _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);  // 10초에서 5초로 감소
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing driver: {ex.Message}");
            _driver?.Quit();
            _driver?.Dispose();
            throw;
        }
    }

    private async Task<IWebElement?> TryFindElement(string selector, int timeoutSeconds = 5)
    {
        if (_driver == null || _wait == null)
            return null;

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

    public async Task SearchNaverAsync(string keyword, bool clickResults = false, int repeatCount = 1, string? targetText = null, string? targetUrl = null)
    {
        for (int attempt = 0; attempt < repeatCount; attempt++)
        {
            try
            {
                if (_driver == null || _wait == null)
                {
                    InitializeDriver();
                }

                if (_driver == null)
                    throw new InvalidOperationException("Failed to initialize WebDriver");

                // Navigate to Naver
                Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}] 검색 시도 {attempt + 1}/{repeatCount} - 네이버로 이동 중...");
                _driver.Navigate().GoToUrl("https://www.naver.com");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 네이버 페이지 로드 완료");
                await Task.Delay(_random.Next(1000, 2000));

                // Try multiple selectors for search box
                IWebElement? searchBox = null;
                foreach (var selector in new[] { 
                    "input[name='query']", 
                    "#query", 
                    "input#query", 
                    "input[title='검색어 입력']" 
                })
                {
                    searchBox = await TryFindElement(selector);
                    if (searchBox != null) break;
                }

                if (searchBox == null)
                    throw new InvalidOperationException("Search box not found");

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 검색창 발견, '{keyword}' 검색어 입력 중...");
                searchBox.Clear();
                await Task.Delay(_random.Next(300, 500));
                searchBox.SendKeys(keyword);
                await Task.Delay(_random.Next(500, 800));
                searchBox.Submit();
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 검색어 입력 완료, 검색 결과 로딩 중...");

                // Wait for search results to load
                await Task.Delay(_random.Next(2000, 3000));

                // 최적화된 선택자로 검색 결과 찾기
                IWebElement? firstResult = null;
                foreach (var selector in new[] {
                    ".place_section a.link_name",  // 지도 검색결과
                    "a.link_tit",                  // 일반 검색결과
                    ".total_wrap a",               // 통합검색 결과
                    "a.api_txt_lines",            // API 검색결과
                    "div.place_section_content a", // 장소 검색결과
                    ".api_txt_lines.total_tit"     // 통합 검색결과 제목
                })
                {
                    firstResult = await TryFindElement(selector, 30);  // Increased timeout to 30 seconds
                    if (firstResult != null) break;
                }

                // If still no results, try broader selectors
                if (firstResult == null)
                {
                    Console.WriteLine("First attempt to find results failed, trying broader selectors...");
                    foreach (var selector in new[] {
                        "div[class*='search_result'] a",
                        "div[class*='content'] a",
                        "div[role='main'] a",
                        "main a"
                    })
                    {
                        firstResult = await TryFindElement(selector, 10);
                        if (firstResult != null) 
                        {
                            Console.WriteLine($"Found result with selector: {selector}");
                            break;
                        }
                    }
                }

                if (firstResult == null)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 검색 결과를 찾을 수 없습니다.");
                    throw new InvalidOperationException("No search results found");
                }
                
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ 검색 결과 발견 성공!");

                if (clickResults)
                    {
                        var results = _driver.FindElements(By.CssSelector("a[href*='map.naver.com'], a.link_tit, .total_wrap a, a.api_txt_lines"))
                            .Where(e => e.Displayed)
                            .ToList();

                        IWebElement? targetElement = null;

                        if (targetText != null || targetUrl != null)
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 특정 검색 결과 찾는 중... (텍스트: {targetText}, URL: {targetUrl})");
                            foreach (var result in results)
                            {
                                try
                                {
                                    string text = result.Text;
                                    string href = result.GetAttribute("href") ?? "";

                                    bool textMatch = targetText == null || text.Contains(targetText);
                                    bool urlMatch = targetUrl == null || href.Contains(targetUrl);

                                    if (textMatch && urlMatch)
                                    {
                                        targetElement = result;
                                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ 원하는 검색 결과를 찾았습니다: {text}");
                                        break;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 결과 확인 중 오류: {ex.Message}");
                                }
                            }

                            if (targetElement == null)
                            {
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 원하는 검색 결과를 찾지 못했습니다.");
                                return;
                            }
                        }
                        else if (results.Any())
                        {
                            var randomIndex = _random.Next(0, results.Count);
                            targetElement = results[randomIndex];

                            try
                            {
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 검색 결과 클릭 시도 중...");
                                ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView(true);", targetElement);
                                await Task.Delay(_random.Next(500, 1500));
                                targetElement.Click();
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ 검색 결과 클릭 성공!");
                                await Task.Delay(_random.Next(5000, 10000));
                                _driver.Navigate().Back();
                                await Task.Delay(_random.Next(1000, 2000));
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error clicking result: {ex.Message}");
                            }
                        }
                    }

                if (attempt < repeatCount - 1)
                {
                    await Task.Delay(_random.Next(10000, 20000));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during search (attempt {attempt + 1}): {ex.Message}");
                
                try
                {
                    InitializeDriver();
                    await Task.Delay(5000);
                }
                catch (Exception driverEx)
                {
                    Console.WriteLine($"Failed to reinitialize driver: {driverEx.Message}");
                }

                if (attempt == repeatCount - 1)
                    throw;
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _driver?.Quit();
                _driver?.Dispose();
            }
            _disposed = true;
        }
    }
}

public class SearchResult
{
    public string LastBuildDate { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Start { get; set; }
    public int Display { get; set; }
    public List<SearchItem> Items { get; set; } = new();
}

public class SearchItem
{
    public string Title { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}