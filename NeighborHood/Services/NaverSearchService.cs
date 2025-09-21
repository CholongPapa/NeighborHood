using System;
using System.Threading.Tasks;

namespace NeighborHood.Services;

public class NaverSearchService : IDisposable
{
    private readonly SearchManager _searchManager;
    private bool _disposed = false;

    public NaverSearchService(IConfiguration configuration)
    {
        _searchManager = new SearchManager(configuration, maxConcurrentSearches: 2);
    }

    public async Task SearchNaverAsync(string keyword, bool clickResults = false, int repeatCount = 1, string? targetText = null, string? targetUrl = null)
    {
        try
        {
            Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}] Starting {repeatCount} searches with parallel execution...");
            await _searchManager.ExecuteSearchesAsync(keyword, repeatCount, targetText, targetUrl);
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] All searches completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during parallel search execution: {ex.Message}");
            throw;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _searchManager.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
