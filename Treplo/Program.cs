﻿using System.Net;
using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using Treplo.Youtube;

namespace Treplo;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        var hostBuilder = Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostCtx, services) =>
            {
                SetupDiscordBot(services, hostCtx);
                services.AddHttpClient(nameof(YoutubeExplorer)).ConfigureHttpMessageHandlerBuilder(x =>
                {
                    var handler = new HttpClientHandler
                    {
                        UseCookies = false
                    };

                    if (handler.SupportsAutomaticDecompression)
                        handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                    x.PrimaryHandler = handler;
                });
                services.AddSingleton<ISearchEngine, YoutubeEngine>();
                services.AddSingleton<ISearchEngineManager, MixedSearchEngineManager>();
                services.AddSingleton<IDateTimeManager, DateTimeManager>();
                services.AddSingleton<IPlayerSessionsManager, PlayerSessionsManager>();
            })
            .UseSerilog((hostingContext, services, loggerConfiguration) =>
            {
                loggerConfiguration
                    .ReadFrom.Configuration(hostingContext.Configuration)
                    .Enrich.FromLogContext()
                    .WriteTo.Console();
            });

        var host = hostBuilder.Build();

        await host.RunAsync();
    }

    private static void SetupDiscordBot(IServiceCollection services, HostBuilderContext hostCtx)
    {
        services.Configure<DiscordClientSettings>(
            hostCtx.Configuration.GetSection(nameof(DiscordClientSettings)));
        services.AddOptions<DiscordClientSettings>();
        services.AddSingleton(ctx =>
        {
            var settings = ctx.GetRequiredService<IOptions<DiscordClientSettings>>();
            var client = new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = settings.Value.Intents
                    .Aggregate(GatewayIntents.None, (seed, cur) => seed | cur)
            });

            return client;
        });
        services.AddSingleton(ctx =>
            {
                var interactionService = new InteractionService(ctx.GetRequiredService<DiscordSocketClient>());
                interactionService.AddModulesAsync(Assembly.GetExecutingAssembly(), ctx);
                return interactionService;
            }
        );
        services.AddHostedService<DiscordBotRunner>();
    }
}