using System.Reflection;
using System.Text.Json;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using dotenv.net;
using Microsoft.Extensions.DependencyInjection;
using Tomlyn;

namespace ZerverBot;

using Microsoft.EntityFrameworkCore.Query.Internal;
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
        Client.SlashCommandExecuted += ExecuteCommand;
        InteractionService.SlashCommandExecuted += HandleCommandExecution;

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

    private static async Task ExecuteCommand(SocketSlashCommand command)
    {
        var context = new SocketInteractionContext(Client, command);
        await InteractionService.ExecuteCommandAsync(context, ServiceProvider);
    }

    private static async Task HandleCommandExecution(SlashCommandInfo info, Discord.IInteractionContext context, IResult result)
    {
        if (result.IsSuccess) return;

        var message = result switch
        {
            ExecuteResult { Exception: not null } exec => $"Error occurred during command: {exec.Exception.InnerException?.Message}",
            _ => $"{result.Error}: {result.ErrorReason}"
        };

        if (!context.Interaction.HasResponded)
        {
            await context.Interaction.RespondAsync(message, ephemeral: true);
        }
        Console.WriteLine(message);
    }

    private static Task Log(LogMessage message)
    {
        Console.WriteLine(message.ToString());
        return Task.CompletedTask;
    }
}