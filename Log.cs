namespace LivrePrintAgent;

/// <summary>Log simples em %LOCALAPPDATA%\LivrePrintAgent\agent.log (reinicia quando passa de 1 MB).</summary>
public static class Log
{
    private static readonly object Gate = new();

    public static string Directory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LivrePrintAgent");

    public static string FilePath => Path.Combine(Directory, "agent.log");

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message) => Write("ERRO", message);

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                System.IO.Directory.CreateDirectory(Directory);

                var file = new FileInfo(FilePath);
                if (file.Exists && file.Length > 1_000_000)
                {
                    File.Move(FilePath, FilePath + ".old", overwrite: true);
                }

                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Log nunca deve derrubar o agente.
        }
    }
}
