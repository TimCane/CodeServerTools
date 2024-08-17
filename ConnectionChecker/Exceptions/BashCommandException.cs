using System;

namespace ConnectionChecker.Exceptions;

public class BashCommandException : Exception
{
    public BashCommandException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
