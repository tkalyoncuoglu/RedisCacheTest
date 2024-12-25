using System.Text.Json;
using MessagePack;
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

var fiveMillionObjects2 = new List<Data>();
for (int i = 0; i < 5_000_000; i++)
{
    fiveMillionObjects2.Add(new Data { Id = Guid.NewGuid() });
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


app.MapGet("/normal-cache2", async (IConnectionMultiplexer redis) =>
{
    var cachedResponse = await redis.GetDatabase().StringGetAsync("normal-cache2");
    if (cachedResponse.HasValue)
    {
        return Serializer.DeserializeFromBinary<List<Data>>(cachedResponse);
    }

    var json = Serializer.SerializeToBinary(fiveMillionObjects2);

    var db = redis.GetDatabase();
    await db.StringSetAsync("normal-cache2", json, TimeSpan.FromMinutes(10));

    return fiveMillionObjects2;
});

 app.UseMiddleware<ResponseCacheMiddleware>();

 app.MapGet("/middleware-cache", () =>
 {
     return fiveMillionObjects;
 });

app.Run();

record O(Guid Id);


[MessagePackObject] 
public class Data
{
    [Key(0)]
    public Guid Id { get; set; }
}

public class Serializer
{
    // Binary JSON (MessagePack)
    public static byte[] SerializeToBinary<T>(T obj)
    {
        return MessagePackSerializer.Serialize(obj);
    }

    public static T DeserializeFromBinary<T>(byte[] data)
    {
        return MessagePackSerializer.Deserialize<T>(data);
    }
}