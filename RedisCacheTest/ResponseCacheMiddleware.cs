namespace RedisCacheTest;

using Microsoft.AspNetCore.Http;
using StackExchange.Redis;
using System.IO;
using System.Threading.Tasks;

public class ResponseCacheMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDatabase _redisDatabase;

    public ResponseCacheMiddleware(RequestDelegate next, IConnectionMultiplexer redis)
    {
        _next = next;
        _redisDatabase = redis.GetDatabase();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string cacheKey = GenerateCacheKey(context);

        // Check if the response is cached in Redis
        try
        {
            var cachedResponse = await _redisDatabase.StringGetAsync(cacheKey);
            if (cachedResponse.HasValue)
            {
                // If the response is cached, write the cached bytes directly to the response body
                context.Response.ContentType = "application/json"; // Set appropriate content type
                await context.Response.Body.WriteAsync(cachedResponse);
                return;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        // Backup the original response body stream
        var originalBodyStream = context.Response.Body;

        try
        {
            using var memoryStream = new MemoryStream();
            context.Response.Body = memoryStream;

            // Call the next middleware in the pipeline
            await _next(context);

            // Reset the memory stream position to read the response
            memoryStream.Seek(0, SeekOrigin.Begin);
            var responseBytes = memoryStream.ToArray();

            // Cache the response bytes in Redis (raw byte array, no string conversion)
            try
            {
                await _redisDatabase.StringSetAsync(cacheKey, responseBytes, TimeSpan.FromMinutes(10));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            // Reset the memory stream position again to copy it to the original response
            memoryStream.Seek(0, SeekOrigin.Begin);
            await memoryStream.CopyToAsync(originalBodyStream);
        }
        finally
        {
            // Restore the original response body stream
            context.Response.Body = originalBodyStream;
        }
    }

    private string GenerateCacheKey(HttpContext context)
    {
        // Example: Use the request path and query string as the cache key
        return $"ResponseCache:{context.Request.Path}{context.Request.QueryString}";
    }
}