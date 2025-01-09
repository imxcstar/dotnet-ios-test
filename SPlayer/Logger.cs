namespace SPlayer;

public static class Logger
{
    public static void Log(string message)
    {
        Console.WriteLine($"[SPlayer] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
    }
}