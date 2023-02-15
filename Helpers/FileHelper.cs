using ForumDataMigration.Extensions;
using Npgsql;

namespace ForumDataMigration.Helpers;

public static class FileHelper
{
    public static async Task CombineMultipleFilesIntoSingleFileAsync(string inputDirectoryPath, string inputFileNamePattern, string outputFilePath, CancellationToken cancellationToken = default)
    {
        var inputFilePaths = Directory.GetFiles(inputDirectoryPath, inputFileNamePattern, SearchOption.AllDirectories);
        Console.WriteLine("Number of files: {0}.", inputFilePaths.Length);
        await using var outputStream = File.Create(outputFilePath);

        foreach (var inputFilePath in inputFilePaths)
        {
            await using (var inputStream = File.OpenRead(inputFilePath))
            {
                // Buffer size can be passed as the second argument.
                await inputStream.CopyToAsync(outputStream, cancellationToken);
            }

            File.Delete(inputFilePath);

            Console.WriteLine("The file {0} has been processed.", inputFilePath);
        }
    }

    public static void ExecuteAllSqlFiles(string inputDirectoryPath, string connectionStr)
    {
        if (!Directory.Exists(inputDirectoryPath))
            return;

        var inputFilePaths = Directory.GetFiles(inputDirectoryPath, "*.sql", SearchOption.AllDirectories).OrderBy(x => x).ToArray();

        var totalFileCount = inputFilePaths.Length;

        Console.WriteLine("Number of files: {0}.", totalFileCount);

        using var connection = new NpgsqlConnection(connectionStr);

        foreach (var inputFilePath in inputFilePaths)
        {
            Console.WriteLine("Number of files left: {0}.", totalFileCount--);

            connection.ExecuteAllTexts(inputFilePath);
        }

        //
        // Parallel.ForEach(inputFilePaths, CommonHelper.GetParallelOptions(CancellationToken.None),
        //                  inputFilePath =>
        //                  {
        //                      using var connection = new NpgsqlConnection(connectionStr);
        //
        //                      connection.ExecuteAllTexts(inputFilePath);
        //                  });
    }
}