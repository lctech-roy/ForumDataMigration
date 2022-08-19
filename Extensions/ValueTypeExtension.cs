namespace ForumDataMigration.Extensions;

public static class ValueTypeExtension
{
    public static string ToCopyValue(this string? value)
    {
        return value ?? Setting.COPY_NULL;
    }
    
    public static string ToCopyValue(this DateTimeOffset? value)
    {
        return value.HasValue ? value.ToString()! : Setting.COPY_NULL;
    }
    
    public static string ToCopyValue(this long? value)
    {
        return value.HasValue ? value.ToString()! : Setting.COPY_NULL;
    }
}