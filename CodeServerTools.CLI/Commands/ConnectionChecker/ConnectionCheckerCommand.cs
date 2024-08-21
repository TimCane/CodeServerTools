using System.CommandLine;
using System.Text.RegularExpressions;
using System.Timers;
using CodeServerTools.CLI.Enums;
using CodeServerTools.CLI.Exceptions;
using CodeServerTools.CLI.Structs;
using Microsoft.Extensions.Logging;
using Timer = System.Timers.Timer;
using System;
using System.Diagnostics;
using System.Linq;

namespace CodeServerTools.CLI.Commands.ConnectionChecker;

public class ConnectionCheckerCommand : Command<ConnectionCheckerCommandOptions, ConnectionCheckerCommandOptionsHandler>
{
    // Keep the hard dependency on System.CommandLine here
    public ConnectionCheckerCommand()
        : base("ConnectionChecker", "Shuts down the server after a period in inactivity")
    {
        this.AddOption(new Option<int>(
                name: "--check-frequency",
                description: "How often the application checks for an active connection (in Seconds)",
                getDefaultValue: () => 5));

        this.AddOption(new Option<int>(
        name: "--grace-period",
        description: "How long the application waits to start the shutdown process (in Seconds)",
        getDefaultValue: () => 30));
    }
}

public class ConnectionCheckerCommandOptions : ICommandOptions
{
    // Automatic binding with System.CommandLine.NamingConventionBinder
    public int CheckFrequency { get; set; } = 5;
    public int GracePeriod { get; set; } = 30;
}

public partial class ConnectionCheckerCommandOptionsHandler : ICommandOptionsHandler<ConnectionCheckerCommandOptions>
{
    [GeneratedRegex("Client connections    : (\\d+)")]
    private partial Regex ClientConnectionsRegex();

    [GeneratedRegex("Shutdown scheduled for .*, use 'shutdown -c' to cancel.")]
    private partial Regex ShutdownScheduledRegex();

    public TunnelId TunnelId { get; set; }
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public bool IsShuttingDown { get; set; } = false;
    public Timer? Timer { get; set; }

    public ConnectionCheckerCommandOptions Options { get; set; }

    private readonly ILogger<ConnectionCheckerCommandOptionsHandler> _logger;

    public ConnectionCheckerCommandOptionsHandler(ILogger<ConnectionCheckerCommandOptionsHandler> logger)
    {
        this._logger = logger;
        this.Options = new ConnectionCheckerCommandOptions();
    }

    public Task<int> HandleAsync(ConnectionCheckerCommandOptions options, CancellationToken cancellationToken)
    {
        Options = options;
        _logger.LogInformation("Application Started");

        var tunnelId = GetTunnelId();
        if (tunnelId == null)
        {
            _logger.LogCritical("No Tunnel found, exiting with code {code}", ExitCode.NoTunnel);
            Environment.Exit((int)ExitCode.NoTunnel);
        }


        _logger.LogInformation("Found tunnel '{tunnelId}'", tunnelId.Value);
        TunnelId = tunnelId.Value;

        StartTimer();
        Console.Read();


        return Task.FromResult(0);
    }

    public void StartTimer()
    {
        var checkFrequencyMs = Options.CheckFrequency * 60 * 1000;
        Timer = new Timer(checkFrequencyMs);
        Timer.AutoReset = true;
        Timer.Elapsed += new ElapsedEventHandler(CheckForConnections);

        _logger.LogInformation("Starting Check Connection Timer with an interval of {frequency}ms", checkFrequencyMs);
        Timer.Start();
    }


    public void CheckForConnections(object? sender, ElapsedEventArgs e)
    {
        _logger.LogInformation("Checking connections for TunnelId {tunnelId}", TunnelId);

        if (HasConnections(TunnelId))
        {
            _logger.LogInformation("TunnelId '{tunnelId}' has connections", TunnelId);
            LastSeen = DateTime.UtcNow;

            if (IsShuttingDown)
            {
                Console.Beep();
                Console.Beep();
                CancelShutdown();
                IsShuttingDown = false;
                _logger.LogInformation("Shutdown canceled due to active connection");
            }
        }
        else
        {
            _logger.LogInformation("No active connections found for TunnelId '{tunnelId}'", TunnelId);

            var elapsed = (DateTime.UtcNow - LastSeen).TotalMilliseconds;
            if (elapsed < Options.GracePeriod * 60 * 1000)
            {
                _logger.LogInformation("Still within grace period ({elapsed}ms), no shutdown initiated", elapsed);
                return;
            }

            if (IsShuttingDown)
            {
                _logger.LogInformation("Already shutting down, no further action needed");
                return;
            }

            _logger.LogInformation("Initiating shutdown due to inactivity");

            Console.Beep();
            InitiateShutdown();
            IsShuttingDown = true;
        }
    }

    private void InitiateShutdown()
    {
        var output = ExecShutdown(Options.GracePeriod * 60 * 1000);
        if (string.IsNullOrEmpty(output))
        {
            _logger.LogWarning("Shutdown command did not return any output");
            return;
        }

        var match = ShutdownScheduledRegex().Match(output);
        if (match == null)
        {
            _logger.LogWarning("No valid shutdown schedule found in output");
            return;
        }

        _logger.LogInformation("Shutdown scheduled successfully");
    }

    private void CancelShutdown()
    {
        _logger.LogInformation("Canceling shutdown");
        ExecCancelShutdown();
    }

    private TunnelId? GetTunnelId()
    {
        _logger.LogInformation("Fetching TunnelId from Bash");
        var output = ExecDevTunnelList();
        if (string.IsNullOrEmpty(output))
        {
            _logger.LogWarning("No output from Bash when fetching TunnelId");
            return null;
        }

        var splitByLine = output.Split('\n');
        if (splitByLine == null)
        {
            _logger.LogWarning("No lines in the output from Bash when fetching TunnelId");
            return null;
        }

        splitByLine = splitByLine.Where(x => !string.IsNullOrEmpty(x)).ToArray();
        if (splitByLine.Length == 0)
        {
            _logger.LogWarning("output only contained empty lines when fetching TunnelId");
        }


        var lastLine = splitByLine.Last();
        if (string.IsNullOrWhiteSpace(lastLine))
        {
            _logger.LogWarning("Last line in Bash output is empty when fetching TunnelId");
            return null;
        }

        var index = lastLine.IndexOf(' ');
        if (index == -1)
        {
            _logger.LogWarning("No space found in the last line when fetching TunnelId");
            return null;
        }

        var strTunnelId = lastLine[..index];
        if (string.IsNullOrWhiteSpace(strTunnelId))
        {
            _logger.LogWarning("Extracted TunnelId string is empty");
            return null;
        }

        _logger.LogInformation("TunnelId fetched successfully: '{tunnelId}'", strTunnelId);
        return new TunnelId(strTunnelId);
    }

    private bool HasConnections(TunnelId tunnelId)
    {
        _logger.LogInformation("Checking if TunnelId '{tunnelId}' is connected", tunnelId);
        var output = ExecDevTunnelShow(tunnelId.Value);
        if (string.IsNullOrEmpty(output))
        {
            _logger.LogWarning("No output from Bash when checking connection for TunnelId '{tunnelId}'", tunnelId);
            return false;
        }

        var match = ClientConnectionsRegex().Match(output);
        if (match == null || !match.Success || match.Groups.Count != 2)
        {
            _logger.LogWarning("Failed to match client connections in the output for TunnelId '{tunnelId}'", tunnelId);
            return false;
        }

        if (int.TryParse(match.Groups[1].Value, out var connections))
        {
            _logger.LogInformation("TunnelId '{tunnelId}' has {connections} connections", tunnelId, connections);
            return connections != 0;
        }

        _logger.LogWarning("Failed to parse number of connections for TunnelId '{tunnelId}'", tunnelId);
        return false;
    }

    internal string ExecDevTunnelList()
    {

        var command = "devtunnel list";

        _logger.LogInformation("Executing '{command}' command.", command);

        try
        {
            var result = RunCommandWithBash(command);
            if (result == string.Empty)
            {
                throw new Exception("No output from command");
            }

            _logger.LogInformation("'{command}' command executed successfully.", command);
            return result;

        }
        catch (Exception ex)
        {
            throw new BashCommandException("Unable to get all dev tunnels", ex);
        }
    }

    internal string ExecDevTunnelShow(string tunnelId)
    {
        var command = $"devtunnel show {tunnelId}";

        _logger.LogInformation("Executing '{command}' command.", command);
        try
        {
            string result = RunCommandWithBash(command);
            _logger.LogInformation("'{command}' command executed successfully.", command);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error executing '{command}' command. {message}", command, ex.Message);
            throw new BashCommandException($"Unable to show tunnel '{tunnelId}'", ex);
        }
    }

    internal string ExecShutdown(int gracePeriod)
    {
        var command = $"shutdown -h {gracePeriod}";
        _logger.LogInformation($"Executing '{command}' command.", command);

        try
        {
            string result = RunCommandWithBash(command);
            _logger.LogInformation("'{command}' command executed successfully.", command);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error executing '{command}' command: {message}", command, ex.Message);
            throw new BashCommandException($"Unable to shutdown", ex);
        }
    }

    internal void ExecCancelShutdown()
    {
        var command = $"shutdown -c";
        _logger.LogInformation("Executing '{command}' command.", command);
        try
        {
            RunCommandWithBash(command);
            _logger.LogInformation("'{command}' command executed successfully.", command);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error executing '{command}' command: {message}", command, ex.Message);
            throw new BashCommandException($"Unable to shutdown", ex);
        }
    }

    private string RunCommandWithBash(string command)
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