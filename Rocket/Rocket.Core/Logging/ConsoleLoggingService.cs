using System;
using Rocket.API.Logging;

namespace Rocket.Core.Logging;

public class ConsoleLoggingService : ILoggingService
{
    public virtual void LogError(string? message, ConsoleColor color)
    {
        Log(message, color);
    }

    public virtual void LogWarning(string? message, ConsoleColor color)
    {
        Log(message, color);
    }

    public virtual void Log(string? message, ConsoleColor color)
    {
        var previousColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ForegroundColor = previousColor;
    }
}