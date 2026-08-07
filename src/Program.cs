using System.Reflection;
using System.Text.Json;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using dotenv.net;
using Microsoft.Extensions.DependencyInjection;
using Tomlyn;

namespace ZerverBot;

using ZerverBot.Commands;

public static class Program
{
    private static readonly IServiceProvider ServiceProvider;
    private static readonly DiscordSocketClient Client;
    private static readonly InteractionService InteractionService;
    private static readonly BotConfig Config;

    static Program()
    {
        DotEnv.Load();
        ServiceProvider = CreateProvider();
        Client = ServiceProvider.GetRequiredService<DiscordSocketClient>();
        InteractionService = ServiceProvider.GetRequiredService<InteractionService>();
        Config = ServiceProvider.GetRequiredService<BotConfig>();
    }


    private static ServiceProvider CreateProvider()
    {
        var clientConfig = new DiscordSocketConfig()
        {
            GatewayIntents = GatewayIntents.AllUnprivileged
        };

        var configSource = File.ReadAllText("config.toml");
        var botConfig = TomlSerializer.Deserialize<BotConfig>(configSource, new TomlSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        }) ?? throw new Exception("Failed to parse bot config!");

        return new ServiceCollection()
            .AddSingleton(clientConfig)
            .AddSingleton<DiscordSocketClient>()
            .AddSingleton(provider => new InteractionService(provider.GetRequiredService<DiscordSocketClient>().Rest))
            .AddSingleton(botConfig)
            .AddSingleton<Ledger>()
            .AddSingleton<AdminLog>()
            .AddSingleton<ArenaService>()
            .BuildServiceProvider();
    }

    public static async Task Main()
    {

        Client.Log += Log;
        Client.Ready += Ready;
        Client.InteractionCreated += HandleInteraction;

        var token = Environment.GetEnvironmentVariable("TOKEN");

        await Client.LoginAsync(TokenType.Bot, token);
        await Client.StartAsync();

        await Task.Delay(-1);
    }

    private static async Task Ready()
    {
        await InteractionService.AddModulesAsync(Assembly.GetEntryAssembly(), ServiceProvider);
        await InteractionService.RegisterCommandsToGuildAsync(1531166559148445766);
    }

    private static async Task HandleInteraction(SocketInteraction interaction)
    {
        try
        {
            var context = new SocketInteractionContext(Client, interaction);
            var result = await InteractionService.ExecuteCommandAsync(context, ServiceProvider);
            if (result.Error != null)
            {
                Console.WriteLine($"Error from a command: {result.Error}");
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
            if (interaction.Type == InteractionType.ApplicationCommand)
            {
                await interaction.FollowupAsync($"Error: {exception}");
            }
        }
    }

    private static Task Log(LogMessage message)
    {
        Console.WriteLine(message.ToString());
        return Task.CompletedTask;
    }
}