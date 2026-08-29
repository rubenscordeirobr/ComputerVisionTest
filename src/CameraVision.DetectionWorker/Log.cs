namespace CameraVision;

public static class Log
{
    private static readonly Lock _lock = new();

    public static void Info(string source, string message) => Write("INFO ", ConsoleColor.Gray, source, message);
    public static void Warn(string source, string message) => Write("WARN ", ConsoleColor.Yellow, source, message);
    public static void Error(string source, string message) => Write("ERROR", ConsoleColor.Red, source, message);
    public static void Ffmpeg(string source, string message) => Write("FFMPG", ConsoleColor.DarkGray, source, message);

    private static void Write(string level, ConsoleColor color, string source, string message)
    {
        lock (_lock)
        {
            Console.ForegroundColor = color;
            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [{level}] [{source}] {message}");
            Console.ResetColor();
        }
    }
}
