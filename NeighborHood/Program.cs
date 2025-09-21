using NeighborHood.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<NaverSearchService>();

var app = builder.Build();

app.MapGet("/", () => "Hello World!");
app.MapGet("/search/{keyword}", async (string keyword, NaverSearchService searchService, int repeat = 1, string? targetText = null, string? targetUrl = null) =>
{
    await searchService.SearchNaverAsync(keyword, clickResults: true, repeatCount: repeat, targetText: targetText, targetUrl: targetUrl);
    return Results.Ok(new { message = $"Search completed {repeat} times for: {keyword}", targetText = targetText, targetUrl = targetUrl });
});

app.Run();
