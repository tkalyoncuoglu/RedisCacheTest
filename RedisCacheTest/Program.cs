using System.Text.Json;
using RedisCacheTest;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect("localhost:6379"));

var app = builder.Build();

var fiveMillionObjects = new List<O>();
for (int i = 0; i < 5_000_000; i++)
{
    fiveMillionObjects.Add(new O(Guid.NewGuid()));
}

app.MapGet("/normal-cache", async (IConnectionMultiplexer redis) =>
{
    var cachedResponse = await redis.GetDatabase().StringGetAsync("normal-cache");
    if (cachedResponse.HasValue)
    {
        return JsonSerializer.Deserialize<List<O>>(cachedResponse);
    }

    var json = JsonSerializer.Serialize(fiveMillionObjects);

    var db = redis.GetDatabase();
    await db.StringSetAsync("normal-cache", json, TimeSpan.FromMinutes(10));

    return fiveMillionObjects;
});

// app.UseMiddleware<ResponseCacheMiddleware>();
//
// app.MapGet("/middleware-cache", () =>
// {
//     return fiveMillionObjects;
// });

app.Run();

record O(Guid Id);