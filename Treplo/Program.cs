﻿using System.Reflection;
using System.Text;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using Treplo.Clients;
using Treplo.Infrastructure.Configuration;

namespace Treplo;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .CreateBootstrapLogger();
        var hostBuilder = Host.CreateDefaultBuilder(args)
            .BindOption<SearchServiceClientSettings>()
            .BindOption<PlayerServiceClientSettings>()
            .BindOption<DiscordClientSettings>()
            .UseOrleansClient(
                siloBuilder =>
                {
                    //TODO: need way of configuring cluster endpoints, id and service id
                    siloBuilder.UseLocalhostClustering();
                }
            )
            .ConfigureServices(
                (hostCtx, services) =>
                {
                    services.AddHttpClient();
                    SetupDiscordBot(services, hostCtx);
                    services.AddSingleton<IDateTimeManager, DateTimeManager>();

                    services.AddTransient<SearchServiceClient>();
                    services.AddSingleton<PlayerServiceClient>();
                    services.AddSingleton<SessionManager>();
                }
            )
            .UseSerilog(
                (hostingContext, _, loggerConfiguration) =>
                {
                    Console.OutputEncoding = Encoding.UTF8;
                    loggerConfiguration.ReadFrom.Configuration(hostingContext.Configuration);
                }
            )
            .ConfigureAppConfiguration(x => x.AddUserSecrets(Assembly.GetExecutingAssembly(), true));

        var host = hostBuilder.Build();

        await host.RunAsync();
    }

    private static void SetupDiscordBot(IServiceCollection services, HostBuilderContext hostCtx)
    {
        services.AddSingleton(
            ctx =>
            {
                var settings = ctx.GetRequiredService<IOptions<DiscordClientSettings>>();
                var client = new DiscordSocketClient(
                    new DiscordSocketConfig
                    {
                        GatewayIntents = settings.Value.Intents
                            .Aggregate(
                                GatewayIntents.None,
                                (seed, cur) => seed | cur
                            ),
                    }
                );

                return client;
            }
        );
        services.AddSingleton(
            ctx =>
            {
                var interactionService =
                    new InteractionService(ctx.GetRequiredService<DiscordSocketClient>());
                interactionService.AddModulesAsync(Assembly.GetExecutingAssembly(), ctx);
                return interactionService;
            }
        );
        services.AddHostedService<DiscordBotRunner>();
    }
}