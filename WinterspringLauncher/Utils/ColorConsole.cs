namespace WinterspringLauncher.Utils;

public static class ColorConsole
{
    public static void Yellow(string message)
    {
        var preColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(message);
        Console.ForegroundColor = preColor;
    }
}
