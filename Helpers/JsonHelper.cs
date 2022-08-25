using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ForumDataMigration.Helpers;

public static class JsonHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
                                                          {
                                                              PropertyNameCaseInsensitive = true,
                                                              DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                                                              WriteIndented = false,
                                                              PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                                                              ReferenceHandler = ReferenceHandler.IgnoreCycles
                                                          };
    
    public static async Task<string> GetJsonAsync<T>(T value, CancellationToken cancellationToken = default)
    {
        var ms = new MemoryStream();

        await JsonSerializer.SerializeAsync(ms, value, JsonOptions, cancellationToken);

        return Encoding.UTF8.GetString(ms.ToArray());
    }
    
    [SuppressMessage("Usage", "VSTHRD103:Call async methods when in an async method")]
    public static async Task<T?> FromJsonAsync<T>(string utf8JsonString, CancellationToken cancellationToken = default)
    {
        var bytes = Encoding.UTF8.GetBytes(utf8JsonString);
        
        using var ms = new MemoryStream();
        
        ms.Write(bytes, 0, bytes.Length);
        ms.Seek(0, SeekOrigin.Begin);

        var result = await JsonSerializer.DeserializeAsync<T>(ms, JsonOptions, cancellationToken);

        return result;
    }
}