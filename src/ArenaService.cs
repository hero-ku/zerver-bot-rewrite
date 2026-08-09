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
    private readonly AdminLog adminLog;
    private readonly BotConfig config;

    private readonly SocketGuild guild;
    private readonly ITextChannel channel;
    private readonly IRole role;

    private readonly Task task;
    private readonly CancellationTokenSource cancellationTokenSource;
    private Boolean isPaused = false;

    public ArenaService(DiscordSocketClient client, AdminLog adminLog, BotConfig config)
    {
        this.adminLog = adminLog;
        this.config = config;

        guild = client.GetGuild(config.GuildId);
        channel = guild.GetTextChannel(config.ArenaChannelId);
        role = guild.GetRole(config.ArenaRoleId);

        cancellationTokenSource = new CancellationTokenSource();
        task = new Task(() =>
        {
            StartPeriodLoop(config.OstracismPeriod, HoldOstracism, cancellationTokenSource.Token);
            StartDailyLoop(config.ElectionTimeOfDay, HoldElection, cancellationTokenSource.Token);
        });
    }

    public Task StartEvent()
    {
        task.Start();
        return Task.CompletedTask;
    }

    public bool PauseOrResumeEvent()
    {
        isPaused = !isPaused;
        return isPaused;
    }

    private async Task EndEvent(SocketGuildUser winner)
    {
        // Add participants to commons
        var commons = guild.GetChannel(config.CommonsCategoryId);
        await commons.RemovePermissionOverwriteAsync(role);

        // REmove participants from arena channel
        await channel.RemovePermissionOverwriteAsync(role);

        await adminLog.AnnounceAsync($"{winner.Mention} is the winner of the Arena!");

        await cancellationTokenSource.CancelAsync();
    }

    private Task StartDailyLoop(int timeInMinutes, Func<Task> action, CancellationToken token)
    {
        return Task.Run(async () =>
        {
            while (true)
            {
                var now = DateTime.Now;
                var next = now.Date.AddMinutes(timeInMinutes);
                if (next <= now)
                    next = now.Date.AddDays(1).AddMinutes(timeInMinutes);

                await Task.Delay(next - now, token);
                if (token.IsCancellationRequested)
                {
                    return Task.FromCanceled(token);
                }
                if (isPaused)
                {
                    continue;
                }
                try
                {
                    await action();
                }
                catch (Exception exception)
                {
                    try
                    {
                        await adminLog.LogAsync($"Error occurred while holding election: {exception}");
                    }
                    catch (Exception logException)
                    {
                        Console.WriteLine(logException);
                    }
                }
            }
        });
    }

    private Task StartPeriodLoop(int periodInMinutes, Func<Task> action, CancellationToken token)
    {
        return Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                var currentTime = DateTime.Now;
                var currentMinute = currentTime.Hour * 60 + currentTime.Minute;

                var lastPeriodMinute = currentMinute - (currentMinute % periodInMinutes);
                var nextPeriodMinute = lastPeriodMinute + periodInMinutes;

                var nextPeriod = currentTime.Date.AddMinutes(nextPeriodMinute);

                try
                {
                    await Task.Delay(nextPeriod - currentTime, token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                try
                {
                    await action();
                }
                catch (Exception exception)
                {
                    try
                    {
                        await adminLog.LogAsync($"Error occurred while holding ostracism: {exception}");
                    }
                    catch (Exception logException)
                    {
                        Console.WriteLine(logException);
                    }
                }
            }
        }, token);
    }

    public async Task HoldOstracism()
    {
        await using var db = new BotContext();
        var votes = await db.OstracismVotes.ToListAsync();

        await db.Users.Where(u => db.OstracismVotes.Any(v => v.VoterId == u.Id)).ExecuteUpdateAsync(setters =>
        {
            setters.SetProperty(u => u.Points, u => u.Points + config.OstracismVotingReward);
        });

        foreach (var vote in votes)
        {
            var voter = guild.GetUser(vote.VoterId);
            if (voter is not null)
            {
                try
                {
                    var dmChannel = await voter.CreateDMChannelAsync();
                    await dmChannel.SendMessageAsync($"You have been awarded **{config.OstracismVotingReward:N0} points** for **voting in the ostracism**.");
                }
                catch (Exception exception)
                {
                    await adminLog.LogAsync(exception.ToString());
                }
            }
        }

        var highestVotes = votes
            .GroupBy(v => v.TargetId)
            .Select(g => new { Id = g.Key, Total = g.Count() })
            .OrderByDescending(t => t.Total)
            .ToList();
        var userVotes = await GetUserVotes(db.OstracismVotes, guild, separator: "voted for");

        await db.OstracismVotes.ExecuteDeleteAsync();

        var announcement = new StringBuilder();

        if (highestVotes.Count == 0 || highestVotes.Count > 1 && highestVotes[0].Total == highestVotes[1].Total)
        {
            announcement.AppendLine("@everyone");
            announcement.AppendLine();
            announcement.AppendLine("The vote was a tie, so nobody was ostracized!");
            announcement.AppendLine();
            announcement.AppendLine(userVotes);

            await channel.SendMessageAsync(announcement.ToString());
            return;
        }

        var highestVote = highestVotes.First();
        var user = guild.GetUser(highestVote.Id);
        if (user is not null)
        {
            try
            {
                await user.RemoveRoleAsync(role);
            }
            catch (Exception exception)
            {
                await adminLog.LogAsync(exception.ToString());
            }
        }

        announcement.AppendLine("@everyone");
        announcement.AppendLine();
        announcement.AppendLine($"{user?.Mention ?? $"{highestVote.Id}"} has been ostracized with **{highestVote.Total} votes**!");
        announcement.AppendLine();
        announcement.AppendLine(userVotes);

        await channel.SendMessageAsync(announcement.ToString());
    }

    public async Task HoldElection()
    {
        await using var db = new BotContext();
        var votes = await db.ElectionVotes.ToListAsync();

        await db.Users.Where(u => db.ElectionVotes.Any(v => v.VoterId == u.Id)).ExecuteUpdateAsync(setters =>
        {
            setters.SetProperty(u => u.Points, u => u.Points + config.ElectionVotingReward);
        });

        foreach (var vote in votes)
        {
            var voter = guild.GetUser(vote.VoterId);
            if (voter is not null)
            {
                try
                {
                    var dmChannel = await voter.CreateDMChannelAsync();
                    await dmChannel.SendMessageAsync($"You have been awarded **{config.ElectionVotingReward:N0} points** for **voting in the ostracism**.");
                }
                catch (Exception exception)
                {
                    await adminLog.LogAsync(exception.ToString());
                }
            }
        }

        var highestVote = votes.AggregateBy(v => v.TargetId, (id) => new { Id = id, Total = 0 }, (v, _) => v with { Total = v.Total + 1 }).MaxBy((v) => v.Value.Total).Value;
        var userVotes = await GetUserVotes(db.ElectionVotes, guild, separator: "voted for");

        await db.ElectionVotes.ExecuteDeleteAsync();

        var announcement = new StringBuilder();

        if (highestVote.Total != votes.Count)
        {
            announcement.AppendLine("@everyone");
            announcement.AppendLine();
            announcement.AppendLine("Nobody has won the elections!");
            announcement.AppendLine("");
            announcement.AppendLine(userVotes);
            await channel.SendMessageAsync(announcement.ToString());
            return;
        }

        var winner = guild.GetUser(highestVote.Id);

        announcement.AppendLine("@everyone");
        announcement.AppendLine();
        announcement.AppendLine($"{winner.Mention} won the election!");
        announcement.AppendLine();
        announcement.AppendLine(userVotes);
        await channel.SendMessageAsync(announcement.ToString());

        await EndEvent(winner);
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

        if (builder.Length == 0)
        {
            builder.AppendLine("There were no votes.");
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

        if (builder.Length == 0)
        {
            builder.AppendLine("There were no votes.");
        }
        return builder.ToString();
    }
}