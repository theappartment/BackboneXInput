namespace BackboneXInput;

internal static class Log
{
    private static readonly string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BackboneXInput", "logs");
    public static void Write(string message, Exception? exception = null)
    {
        var line = $"{DateTimeOffset.Now:O} {message}" + (exception is null ? "" : $"\n{exception}");
        Console.Error.WriteLine(line);
        try
        {
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"{DateTime.Today:yyyy-MM-dd}.log");
            if (File.Exists(path) && new FileInfo(path).Length > 5_000_000) File.Move(path, path + ".old", true);
            File.AppendAllText(path, line + Environment.NewLine);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Console.Error.WriteLine("Log su file non disponibile: " + ex.Message); }
    }
}
