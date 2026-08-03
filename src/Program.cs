using System.Text.Json;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using dotenv.net;
using Microsoft.Extensions.DependencyInjection;
using Tomlyn;

public class Program
{
    private static readonly IServiceProvider serviceProvider;
    private static readonly DiscordSocketClient client;
    private static readonly InteractionService interactionService;
    private static readonly BotConfig config;

    static Program()
    {
        DotEnv.Load();
        serviceProvider = CreateProvider();
        client = serviceProvider.GetRequiredService<DiscordSocketClient>();
        interactionService = serviceProvider.GetRequiredService<InteractionService>();
        config = serviceProvider.GetRequiredService<BotConfig>();
    }


    private static IServiceProvider CreateProvider()
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
            .BuildServiceProvider();
    }

    public static async Task Main()
    {

        client.Log += Log;
        client.Ready += Ready;
        client.InteractionCreated += HandleInteraction;

        var token = Environment.GetEnvironmentVariable("TOKEN");

        await client.LoginAsync(TokenType.Bot, token);
        await client.StartAsync();

        await Task.Delay(-1);
    }

    private static async Task Ready()
    {
        await interactionService.AddModuleAsync<Points>(serviceProvider);
        await interactionService.RegisterCommandsToGuildAsync(1531166559148445766);
    }

    public static async Task HandleInteraction(SocketInteraction interaction)
    {
        try
        {
            var context = new SocketInteractionContext(client, interaction);
            var result = interactionService.ExecuteCommandAsync(context, serviceProvider);
            if (result.Exception != null)
            {
                Console.WriteLine($"{result.Exception}");
            }
        }
        catch (Exception exception)
        {
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