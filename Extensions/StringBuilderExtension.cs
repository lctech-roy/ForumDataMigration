using System.Text;

namespace ForumDataMigration.Extensions;

public static class StringBuilderExtension
{
    private const string DELIMITER = "";
    
    public static void AppendCopyValues(this StringBuilder sb, params object[] values)
    {
        sb.Append(values[0]);
        
        for (var i = 1; i < values.Length; i++)
        {
            sb.Append(DELIMITER);
            sb.Append(values[i]);
        }

        sb.Append("\n");
    }
}