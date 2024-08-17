using System.Text.RegularExpressions;
using System.Timers;
using ConnectionChecker.Enums;
using ConnectionChecker.Structs;
using Timer = System.Timers.Timer;

namespace ConnectionChecker
{
    internal partial class Program
    {
        [GeneratedRegex("Client connections    : (\\d+)")]
        private static partial Regex ClientConnectionsRegex();

        [GeneratedRegex("Shutdown scheduled for .*, use 'shutdown -c' to cancel.")]
        private static partial Regex ShutdownScheduledRegex();

        public static TunnelId TunnelId { get; set; }
        public static DateTime LastSeen { get; set; } = DateTime.UtcNow;
        public static bool IsShuttingDown { get; set; } = false;
        public static Timer? Timer { get; set; }

        public const int CheckFrequency = 5;
        public const int GracePeriod = 30;

        public const int CheckFrequencyMs = CheckFrequency * 60 * 1000;
        public const int GracePeriodMs = GracePeriod * 60 * 1000;

        static void Main(string[] args)
        {
            AppLogger.Instance.LogInformation("Application Started");

            var tunnelId = GetTunnelId();
            if (tunnelId == null)
            {
                AppLogger.Instance.LogCritical("No Tunnel found, exiting with code {code}", ExitCode.NoTunnel);
                Environment.Exit((int)ExitCode.NoTunnel);
            }


            AppLogger.Instance.LogInformation("Found tunnel '{tunnelId}'", tunnelId.Value);
            TunnelId = tunnelId.Value;

            StartTimer();
            Console.Read();
        }


        public static void StartTimer()
        {
            Timer = new Timer(CheckFrequencyMs);
            Timer.AutoReset = true;
            Timer.Elapsed += new ElapsedEventHandler(CheckForConnections);

            AppLogger.Instance.LogInformation("Starting Check Connection Timer with an interval of {frequency}ms", CheckFrequencyMs);
            Timer.Start();
        }

        public static void CheckForConnections(object? sender, ElapsedEventArgs e)
        {
            AppLogger.Instance.LogInformation("Checking connections for TunnelId {tunnelId}", TunnelId);

            if (HasConnections(TunnelId))
            {
                AppLogger.Instance.LogInformation("TunnelId '{tunnelId}' has connections", TunnelId);
                LastSeen = DateTime.UtcNow;

                if (IsShuttingDown)
                {
                    Console.Beep();
                    Console.Beep();
                    CancelShutdown();
                    IsShuttingDown = false;
                    AppLogger.Instance.LogInformation("Shutdown canceled due to active connection");
                }
            }
            else
            {
                AppLogger.Instance.LogInformation("No active connections found for TunnelId '{tunnelId}'", TunnelId);

                var elapsed = (DateTime.UtcNow - LastSeen).TotalMilliseconds;
                if (elapsed < GracePeriodMs)
                {
                    AppLogger.Instance.LogInformation("Still within grace period ({elapsed}ms), no shutdown initiated", elapsed);
                    return;
                }

                if (IsShuttingDown)
                {
                    AppLogger.Instance.LogInformation("Already shutting down, no further action needed");
                    return;
                }

                AppLogger.Instance.LogInformation("Initiating shutdown due to inactivity");

                Console.Beep();
                InitiateShutdown();
                IsShuttingDown = true;
            }
        }

        private static void InitiateShutdown()
        {
            var output = Bash.ExecShutdown(GracePeriodMs);
            if (string.IsNullOrEmpty(output))
            {
                AppLogger.Instance.LogWarning("Shutdown command did not return any output");
                return;
            }

            var match = ShutdownScheduledRegex().Match(output);
            if (match == null)
            {
                AppLogger.Instance.LogWarning("No valid shutdown schedule found in output");
                return;
            }

            AppLogger.Instance.LogInformation("Shutdown scheduled successfully");
        }

        private static void CancelShutdown()
        {
            AppLogger.Instance.LogInformation("Canceling shutdown");
            Bash.ExecCancelShutdown();
        }

        private static TunnelId? GetTunnelId()
        {
            AppLogger.Instance.LogInformation("Fetching TunnelId from Bash");
            var output = Bash.ExecDevTunnelList();
            if (string.IsNullOrEmpty(output))
            {
                AppLogger.Instance.LogWarning("No output from Bash when fetching TunnelId");
                return null;
            }

            var splitByLine = output.Split('\n');
            if (splitByLine == null || splitByLine.Length == 0)
            {
                AppLogger.Instance.LogWarning("No lines in the output from Bash when fetching TunnelId");
                return null;
            }

            var lastLine = splitByLine.Last();
            if (string.IsNullOrWhiteSpace(lastLine))
            {
                AppLogger.Instance.LogWarning("Last line in Bash output is empty when fetching TunnelId");
                return null;
            }

            var index = lastLine.IndexOf(' ');
            if (index == -1)
            {
                AppLogger.Instance.LogWarning("No space found in the last line when fetching TunnelId");
                return null;
            }

            var strTunnelId = lastLine[..index];
            if (string.IsNullOrWhiteSpace(strTunnelId))
            {
                AppLogger.Instance.LogWarning("Extracted TunnelId string is empty");
                return null;
            }

            AppLogger.Instance.LogInformation("TunnelId fetched successfully: '{tunnelId}'", strTunnelId);
            return new TunnelId(strTunnelId);
        }

        private static bool HasConnections(TunnelId tunnelId)
        {
            AppLogger.Instance.LogInformation("Checking if TunnelId '{tunnelId}' is connected", tunnelId);
            var output = Bash.ExecDevTunnelShow(tunnelId.Value);
            if (string.IsNullOrEmpty(output))
            {
                AppLogger.Instance.LogWarning("No output from Bash when checking connection for TunnelId '{tunnelId}'", tunnelId);
                return false;
            }

            var match = ClientConnectionsRegex().Match(output);
            if (match == null || !match.Success || match.Groups.Count != 2)
            {
                AppLogger.Instance.LogWarning("Failed to match client connections in the output for TunnelId '{tunnelId}'", tunnelId);
                return false;
            }

            if (int.TryParse(match.Groups[1].Value, out var connections))
            {
                AppLogger.Instance.LogInformation("TunnelId '{tunnelId}' has {connections} connections", tunnelId, connections);
                return connections != 0;
            }

            AppLogger.Instance.LogWarning("Failed to parse number of connections for TunnelId '{tunnelId}'", tunnelId);
            return false;
        }
    }
}