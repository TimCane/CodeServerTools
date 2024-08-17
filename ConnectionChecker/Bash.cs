using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using ConnectionChecker.Exceptions;

namespace ConnectionChecker;

public static class Bash
{
    internal static string ExecDevTunnelList()
    {

        var command = "devtunnel list";

        AppLogger.Instance.LogInformation("Executing '{command}' command.", command);

        try
        {
            var result = RunCommandWithBash(command);
            if (result == string.Empty)
            {
                throw new Exception("No output from command");
            }

            AppLogger.Instance.LogInformation("'{command}' command executed successfully.", command);
            return result;

        }
        catch (Exception ex)
        {
            throw new BashCommandException("Unable to get all dev tunnels", ex);
        }
    }

    internal static string ExecDevTunnelShow(string tunnelId)
    {
        var command = $"devtunnel show {tunnelId}";

        AppLogger.Instance.LogInformation("Executing '{command}' command.", command);
        try
        {
            string result = RunCommandWithBash(command);
            AppLogger.Instance.LogInformation("'{command}' command executed successfully.", command);
            return result;
        }
        catch (Exception ex)
        {
            AppLogger.Instance.LogError("Error executing '{command}' command. {message}", command, ex.Message);
            throw new BashCommandException($"Unable to show tunnel '{tunnelId}'", ex);
        }
    }

    internal static string ExecShutdown(int gracePeriod)
    {
        var command = $"shutdown -h {gracePeriod}";
        AppLogger.Instance.LogInformation($"Executing '{command}' command.", command);

        try
        {
            string result = RunCommandWithBash(command);
            AppLogger.Instance.LogInformation("'{command}' command executed successfully.", command);
            return result;
        }
        catch (Exception ex)
        {
            AppLogger.Instance.LogError("Error executing '{command}' command: {message}", command, ex.Message);
            throw new BashCommandException($"Unable to shutdown", ex);
        }
    }

    internal static void ExecCancelShutdown()
    {
        var command = $"shutdown -c";
        AppLogger.Instance.LogInformation("Executing '{command}' command.", command);
        try
        {
            RunCommandWithBash(command);
            AppLogger.Instance.LogInformation("'{command}' command executed successfully.", command);
        }
        catch (Exception ex)
        {
            AppLogger.Instance.LogError("Error executing '{command}' command: {message}", command, ex.Message);
            throw new BashCommandException($"Unable to shutdown", ex);
        }
    }

    private static string RunCommandWithBash(string command)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "/bin/sh",
            Arguments = $"-c \"{command}\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        });

        if (process == null)
        {
            throw new Exception("Unable to lauch process");
        }

        process.WaitForExit(-1);

        return process.StandardOutput.ReadToEnd();
    }
}
