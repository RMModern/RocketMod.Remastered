using System;

namespace Rocket.API.Logging;

public interface ILoggingService
{
    void LogError(string? message, ConsoleColor color = ConsoleColor.Red);
    void LogWarning(string? message, ConsoleColor color = ConsoleColor.Yellow);
    void Log(string? message, ConsoleColor color = ConsoleColor.White);
}