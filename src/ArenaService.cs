using System.Data;
using System.Text;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using ZerverBot.Commands;
using ZerverBot.Model;
using ZerverBot.Model.Arena;

namespace ZerverBot;

public class ArenaService
{
    private readonly DiscordSocketClient client;
    private readonly AdminLog adminLog;
    private readonly InteractionService interactionService;
    private readonly BotConfig config;

    private readonly SocketGuild guild;
    private readonly ITextChannel channel;
    private readonly IRole role;

    private readonly Task task;
    private readonly CancellationTokenSource cancellationTokenSource;

    public ArenaService(DiscordSocketClient client, AdminLog adminLog, InteractionService interactionService, BotConfig config)
    {
        this.client = client;
        this.adminLog = adminLog;
        this.interactionService = interactionService;
        this.config = config;

        guild = client.GetGuild(config.GuildId);
        channel = guild.GetTextChannel(config.ArenaChannelId);
        role = guild.GetRole(config.ArenaRoleId);

        cancellationTokenSource = new CancellationTokenSource();
        task = new Task(() =>
        {
            StartEventLoopAsync(config.OstracismPeriod, HoldOstracism, cancellationTokenSource.Token);
            StartEventLoopAsync(60 * 24, HoldElection, cancellationTokenSource.Token, config.ElectionTimeOfDay * 60);
        });
    }

    public Task StartEvent()
    {
        task.Start();
        return Task.CompletedTask;
    }

    private async Task EndEvent()
    {
        await cancellationTokenSource.CancelAsync();

        var arenaRole = guild.GetRole(config.ArenaRoleId);
        var arenaParticipantRole = guild.GetRole(config.ArenaParticipantRoleId);

        await foreach (var user in guild.GetUsersAsync().Flatten())
        {
            await user.RemoveRoleAsync(arenaRole);
            await user.AddRoleAsync(arenaParticipantRole);
        }
    }

    private static void StartEventLoopAsync(int periodInMinutes, Func<Task> action, CancellationToken token, int offsetInMinutes = 0)
    {
        Task.Run(async () =>
        {
            while (true)
            {
                var currentTime = DateTime.Now;
                var currentMinute = currentTime.Hour * 60 + currentTime.Minute;

                var lastPeriodMinute = currentMinute - (currentMinute % periodInMinutes);
                var nextPeriodMinute = lastPeriodMinute + periodInMinutes;

                var nextPeriod = currentTime.Date.AddMinutes(nextPeriodMinute + offsetInMinutes);
                var timeUntilNextPeriod = nextPeriod - currentTime;

                await Task.Delay(timeUntilNextPeriod, token);
                if (token.IsCancellationRequested)
                {
                    return Task.FromCanceled(token);
                }
                await action();
            }
        }, token);
    }

    public async Task HoldOstracism()
    {
        var db = new BotContext();
        var highestVotes = await db.OstracismVotes
            .GroupBy(v => v.TargetId)
            .Select(g => new { Id = g.Key, Total = g.Count() })
            .OrderByDescending(t => t.Total)
            .Take(2)
            .ToListAsync();

        var announcement = new StringBuilder();

        if (highestVotes.Count != 1)
        {
            announcement.AppendLine("@everyone");
            announcement.AppendLine();
            announcement.AppendLine("The vote was a tie, so nobody was ostracized!");
            announcement.AppendLine();
            announcement.AppendLine(await GetUserVotes(db.OstracismVotes, guild, separator: "voted for"));

            await channel.SendMessageAsync(announcement.ToString());
            return;
        }

        var highestVote = highestVotes.First();
        var user = guild.GetUser(highestVote.Id);

        await user.RemoveRoleAsync(role);

        announcement.AppendLine("@everyone");
        announcement.AppendLine();
        announcement.AppendLine($"{user.Mention} has been ostracized with **{highestVote.Total} votes**!");
        announcement.AppendLine();
        announcement.AppendLine(await GetUserVotes(db.OstracismVotes, guild, separator: "voted for"));
        channel.GetMessagesAsync();
        await channel.SendMessageAsync(announcement.ToString());

        await db.OstracismVotes.ExecuteDeleteAsync();
    }

    public async Task HoldElection()
    {
        var db = new BotContext();
        var highestVote = await db.ElectionVotes
            .GroupBy(v => v.TargetId)
            .Select(g => new { Id = g.Key, Total = g.Count() })
            .OrderByDescending(t => t.Total)
            .FirstOrDefaultAsync();
        var totalVotes = await db.OstracismVotes.CountAsync();

        await db.ElectionVotes.ExecuteDeleteAsync();

        var announcement = new StringBuilder();

        if (highestVote?.Total != totalVotes)
        {
            announcement.AppendLine("@everyone");
            announcement.AppendLine();
            announcement.AppendLine("Nobody has won the elections!");
            announcement.AppendLine("");
            announcement.AppendLine(await GetUserVotes(db.ElectionVotes, guild, separator: "voted for"));
            await channel.SendMessageAsync(announcement.ToString());
            return;
        }

        var winner = guild.GetUser(highestVote.Id);

        announcement.AppendLine("@everyone");
        announcement.AppendLine();
        announcement.AppendLine($"{winner.Mention} won the election!");
        announcement.AppendLine();
        announcement.AppendLine(await GetUserVotes(db.ElectionVotes, guild, separator: "voted for"));
        await channel.SendMessageAsync(announcement.ToString());

        await EndEvent();
        await adminLog.AnnounceAsync($"{winner.Mention} is the winner of the Arena!");
    }

    public static async Task<string> GetVoteCounts<T>(DbSet<T> set, IGuild guild) where T : Vote
    {
        var voteTotals = set
            .GroupBy(v => v.TargetId)
            .Select(g => new { Id = g.Key, Total = g.Count() })
            .OrderByDescending(t => t.Total)
            .AsAsyncEnumerable();

        var builder = new StringBuilder();
        await foreach (var target in voteTotals)
        {
            var user = await guild.GetUserAsync(target.Id);
            builder.AppendLine($"{user.Mention} | {target.Total} votes");
        }

        return builder.ToString();
    }

    public static async Task<string> GetUserVotes<T>(DbSet<T> set, IGuild guild, string separator = "->") where T : Vote
    {
        var userVotes = set
            .OrderByDescending(v => set.Count(x => x.TargetId == v.TargetId))
            .ThenBy(v => v.TargetId)
            .AsAsyncEnumerable();

        var builder = new StringBuilder();
        await foreach (var vote in userVotes)
        {
            var voter = await guild.GetUserAsync(vote.VoterId);
            var target = await guild.GetUserAsync(vote.TargetId);
            builder.AppendLine($"{voter.Mention} {separator} {target.Mention}");
        }

        return builder.ToString();
    }
}