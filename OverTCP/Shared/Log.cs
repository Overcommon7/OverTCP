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


        public static event Action<string, string, Severity>? OnMessagePosted;
        public static event Action<Exception, Exception?>? OnExceptionPosted;
        static string mMessage = string.Empty;
        public static void Message(object? message)
        {
            Message(message?.ToString());
        }
        public static void Warning(object? message)
        {
            Warning(message?.ToString());
        }
        public static void Error(object? message)
        {
            if (message is Exception exception)
                OnExceptionPosted?.Invoke(exception, exception.InnerException);

            Error(message?.ToString());
        }

        public static void Message(string? message)
        {
            if (string.IsNullOrEmpty(message))
                mMessage = "MESSAGE: NULL";
            else
                mMessage = "MESAGE: " + message;
           
            OnMessagePosted?.Invoke(mMessage, string.Empty, Severity.Message);
        }
        public static void Warning(string? message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            if (string.IsNullOrEmpty(message))
                mMessage = "WARNING: NULL";
            else
                mMessage = "WARNING: " + message;

            OnMessagePosted?.Invoke(mMessage, new StackTrace(true).ToString(), Severity.Warning);
            Console.ResetColor();
        }
        public static void Error(string? message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            if (string.IsNullOrEmpty(message))
                mMessage = "ERROR: NULL";
            else
                mMessage = "ERROR: " + message;

            OnMessagePosted?.Invoke(mMessage, new StackTrace(true).ToString(), Severity.Error);
            Console.ResetColor();
        }
    }
}
