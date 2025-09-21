using System.Collections.Concurrent;

namespace NeighborHood.Services;

public class SearchManager : IDisposable
{
    private readonly int _maxConcurrentSearches;
    private readonly ConcurrentDictionary<int, SearchWorker> _workers;
    private readonly SemaphoreSlim _semaphore;
    private readonly ConcurrentQueue<(string keyword, string? targetText, string? targetUrl)> _workQueue;
    private readonly IConfiguration _configuration;
    private bool _disposed;

    public int ActiveWorkerCount => _workers.Count;
    public int QueuedWorkCount => _workQueue.Count;
    public bool HasAvailableWorkers => _semaphore.CurrentCount > 0;

    public SearchManager(IConfiguration configuration, int maxConcurrentSearches = 5)
    {
        _configuration = configuration;
        _maxConcurrentSearches = maxConcurrentSearches;
        _workers = new ConcurrentDictionary<int, SearchWorker>();
        _semaphore = new SemaphoreSlim(maxConcurrentSearches);
        _workQueue = new ConcurrentQueue<(string keyword, string? targetText, string? targetUrl)>();

        ThreadPool.SetMinThreads(maxConcurrentSearches, maxConcurrentSearches);
        ThreadPool.SetMaxThreads(maxConcurrentSearches * 2, maxConcurrentSearches * 2);
    }

    public async Task ExecuteSearchesAsync(string keyword, int repeatCount, string? targetText = null, string? targetUrl = null)
    {
        var tasks = new List<Task>();
        var random = new Random();

        Console.WriteLine($"\n=== 검색 작업 시작 ===");
        Console.WriteLine($"총 {repeatCount}회 검색 예정");
        Console.WriteLine($"동시 실행 제한: {_maxConcurrentSearches}개");
        Console.WriteLine($"검색어: {keyword}");
        if (!string.IsNullOrEmpty(targetText)) Console.WriteLine($"목표 텍스트: {targetText}");
        if (!string.IsNullOrEmpty(targetUrl)) Console.WriteLine($"목표 URL: {targetUrl}");
        Console.WriteLine("==================\n");

        for (int i = 0; i < repeatCount; i++)
        {
            await _semaphore.WaitAsync();

            var workerId = i % _maxConcurrentSearches;
            var worker = _workers.GetOrAdd(workerId, id => new SearchWorker(id, _configuration));

            var task = Task.Run(async () =>
            {
                try
                {
                    await worker.ExecuteSearchAsync(keyword, targetText, targetUrl);
                    await Task.Delay(random.Next(2000, 5000));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in worker {workerId}: {ex.Message}");
                    
                    if (_workers.TryRemove(workerId, out var failedWorker))
                    {
                        failedWorker.Dispose();
                    }
                    
                    _workers.TryAdd(workerId, new SearchWorker(workerId, _configuration));
                }
                finally
                {
                    _semaphore.Release();
                }
            });

            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var worker in _workers.Values)
            {
                worker.Dispose();
            }
            _workers.Clear();
            _semaphore.Dispose();
            _disposed = true;
        }
    }
}