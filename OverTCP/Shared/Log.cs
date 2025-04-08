using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace OverTCP
{
    public static class Log
    {
        public enum Severity
        { 
            Message = 0,
            Warning = 1,
            Error = 2
        }


        public static event Action<string, Severity>? OnMessagePosted;
        static string mMessage = string.Empty;
        public static void Message(object? message)
        {
#if DEBUG
            Message(message?.ToString());
#endif
        }
        public static void Warning(object? message)
        {
            Warning(message?.ToString());
        }
        public static void Error(object? message)
        {
            Error(message?.ToString());
        }

        public static void Message(string? message)
        {
#if DEBUG
            if (string.IsNullOrEmpty(message))
                mMessage = "MESSAGE: NULL";
            else
                mMessage = "MESAGE: " + message;
           
            Console.WriteLine(mMessage);
            OnMessagePosted?.Invoke(mMessage, Severity.Message);
#endif
        }
        public static void Warning(string? message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            if (string.IsNullOrEmpty(message))
                mMessage = "WARNING: NULL";
            else
                mMessage = "WARNING: " + message;

            Console.WriteLine(mMessage);
            Console.WriteLine(new StackTrace(true));
            OnMessagePosted?.Invoke(mMessage, Severity.Warning);
            Console.ResetColor();
        }
        public static void Error(string? message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            if (string.IsNullOrEmpty(message))
                mMessage = "ERROR: NULL";
            else
                mMessage = "ERROR: " + message;

            Console.WriteLine(mMessage);
            Console.WriteLine(new StackTrace(true));
            OnMessagePosted?.Invoke(mMessage, Severity.Error);
            Console.ResetColor();
        }
    }
}
