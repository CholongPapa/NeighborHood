using NeighborHood.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<NaverSearchService>();

var app = builder.Build();

app.MapGet("/", () => "Hello World!");
app.MapGet("/search", async (
    NaverSearchService searchService,
    string? keyword = "늘오던헤어살롱", 
    int repeat = 10, 
    string? targetText = "늘오던헤어살롱", 
    string? targetUrl = "map.naver.com") =>
{
    if (searchService == null) return Results.BadRequest("Search service is not available");
    
    await searchService.SearchNaverAsync(
        keyword ?? "늘오던헤어살롱", 
        clickResults: true, 
        repeatCount: repeat, 
        targetText: targetText ?? "늘오던헤어살롱", 
        targetUrl: targetUrl ?? "map.naver.com");
    
    return Results.Ok(new { 
        message = $"Search completed {repeat} times for: {keyword}", 
        targetText = targetText, 
        targetUrl = targetUrl 
    });
});

app.Run();
