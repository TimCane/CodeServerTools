using System;

namespace CodeServerTools.CLI.Exceptions;

public class BashCommandException : Exception
{
    public BashCommandException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
