namespace ForumDataMigration.Extensions;

public static class ValueTypeExtension
{
    private const string COPY_NULL = "\\N";

    public static string ToCopyValue(this string? value)
    {
        return value ?? COPY_NULL;
    }

    public static string ToCopyValue(this DateTimeOffset? value)
    {
        return value.HasValue ? value.ToString()! : COPY_NULL;
    }

    public static string ToCopyValue(this long? value)
    {
        return value.HasValue ? value.ToString()! : COPY_NULL;
    }
    
    public static string ToCopyValue(this int? value)
    {
        return value.HasValue ? value.ToString()! : COPY_NULL;
    }

    public static string ToCopyValue(this decimal? value)
    {
        return value.HasValue ? value.ToString()! : COPY_NULL;
    }
    
    public static DateTimeOffset? ToDatetimeOffset(this int? value)
    {
        return value.HasValue ? DateTimeOffset.FromUnixTimeSeconds(value.Value)! : null;
    }
}