using System;
using Rocket.Core.Logging;
using SDG.Unturned;

namespace Rocket.Unturned.Logging;

// LDM: originally Rocket called ProcessLog here to write the message to their log file as well as the Console.
// After LDM was updated to use the CommandWindow methods instead of the Console directly this caused each 
// message to be logged twice because Rocket logs the output of CommandWindow.onCommandWindowOutputted.
internal class CommandWindowLoggingService : ConsoleLoggingService
{
    public override void LogError(string? message, ConsoleColor color)
    {
        if (message is null)
            return;
        
        if (CommandWindow.insideExplicitLogging)
            return;
        try
        {
            CommandWindow.insideExplicitLogging = true;
            UnturnedLog.error(message);
            base.LogError(message, color);
            CommandWindow.onCommandWindowOutputted?.Invoke(message, color);
        }
        finally
        {
            CommandWindow.insideExplicitLogging = false;
        }
    }

    public override void LogWarning(string? message, ConsoleColor color)
    {
        if (message is null)
            return;
        
        if (CommandWindow.insideExplicitLogging)
            return;
        try
        {
            CommandWindow.insideExplicitLogging = true;
            UnturnedLog.warn(message);
            base.LogWarning(message, color);
            CommandWindow.onCommandWindowOutputted?.Invoke(message, color);
        }
        finally
        {
            CommandWindow.insideExplicitLogging = false;
        }
    }

    public override void Log(string? message, ConsoleColor color)
    {
        if (message is null)
            return;
        
        if (CommandWindow.insideExplicitLogging)
            return;
        try
        {
            CommandWindow.insideExplicitLogging = true;
            UnturnedLog.info(message);
            base.Log(message, color);
            CommandWindow.onCommandWindowOutputted?.Invoke(message, color);
        }
        finally
        {
            CommandWindow.insideExplicitLogging = false;
        }
    }
}