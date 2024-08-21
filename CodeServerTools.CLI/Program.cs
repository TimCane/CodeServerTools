using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using CodeServerTools.CLI.Commands.ConnectionChecker;
using CodeServerTools.CLI.Middlewares;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CodeServerTools.CLI;

public class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand
        {
            new ConnectionCheckerCommand(),
        };

        var builder = new CommandLineBuilder(rootCommand).UseDefaults().UseDependencyInjection(services =>
        {
            services.AddLogging(options =>
            {
                options.ClearProviders();
                options.AddConsole();
            });
        });

        return await builder.Build().InvokeAsync(args);
    }
}
