using System.Diagnostics.CodeAnalysis;
using System.Text;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

public class Arena(DiscordSocketClient client, BotConfig config, Ledger ledger) : InteractionModuleBase
{
    [SlashCommand("ostracize", "Vote to ostracize a member")]
    public async Task Ostracize(IGuildUser target)
    {
        if (!target.RoleIds.Contains(config.ArenaRoleId))
        {
            await RespondAsync($"{target.Mention} is not in the arena.", ephemeral: true);
        }

        var db = new BotContext();
        var vote = await db.OstracismVotes.Where(v => v.VoterId == Context.User.Id).SingleOrDefaultAsync() ?? new OstracismVote { VoterId = Context.User.Id };
        vote.TargetId = target.Id;
        db.Add(vote);
        await db.SaveChangesAsync();

        await RespondAsync($"You are now voting for {target.Mention} to be ostracized.", ephemeral: true);
    }

    [SlashCommand("elect", "Vote to elect a member")]
    public async Task Elect(IGuildUser target)
    {
        if (!target.RoleIds.Contains(config.ArenaRoleId))
        {
            await RespondAsync($"{target.Mention} is not in the arena.", ephemeral: true);
        }

        var db = new BotContext();
        var vote = await db.ElectionVotes.Where(v => v.VoterId == Context.User.Id).SingleOrDefaultAsync() ?? new ElectionVote { VoterId = Context.User.Id };
        vote.TargetId = target.Id;
        db.Add(vote);
        await db.SaveChangesAsync();

        await RespondAsync($"You are now voting for {target.Mention} to be elected.", ephemeral: true);
    }

    [Group("votes", "Gets the current state of voting")]
    public class VoteCommands : InteractionModuleBase
    {
        [SlashCommand("ostracism", "Gets the current of state of the ostracism")]
        public async Task GetOstracismVotes(bool listUserVotes = false)
        {
            var db = new BotContext();
            await RespondAsync(listUserVotes
                ? await GetUserVotes(db.OstracismVotes)
                : await GetVoteCounts(db.OstracismVotes)
            );
        }

        [SlashCommand("election", "Gets the current of state of the election")]
        public async Task GetElectionVotes(bool listUserVotes = false)
        {
            var db = new BotContext();
            await RespondAsync(listUserVotes
                ? await GetUserVotes(db.ElectionVotes)
                : await GetVoteCounts(db.ElectionVotes)
            );
        }

        public async Task<string> GetVoteCounts<T>(DbSet<T> set) where T : Vote
        {
            var voteTotals = set
            .GroupBy(v => v.TargetId)
            .Select(g => new { Id = g.Key, Votes = g.Count() })
            .OrderByDescending(t => t.Votes)
            .AsAsyncEnumerable();

            var builder = new StringBuilder();
            await foreach (var target in voteTotals)
            {
                var user = await Context.Guild.GetUserAsync(target.Id);
                builder.AppendLine($"{user.Mention} | {target.Votes} votes");
            }

            return builder.ToString();
        }

        public async Task<string> GetUserVotes<T>(DbSet<T> set) where T : Vote
        {
            var builder = new StringBuilder();
            await foreach (var vote in set.AsAsyncEnumerable())
            {
                var voter = await Context.Guild.GetUserAsync(vote.VoterId);
                var target = await Context.Guild.GetUserAsync(vote.TargetId);
                builder.AppendLine($"{voter.Mention} -> {target.Mention}");
            }

            return builder.ToString();
        }
    }

    [SlashCommand("enter", "The arena awaits...")]
    public async Task EnterArena()
    {
        var guildUser = await Context.Guild.GetUserAsync(Context.User.Id);
        var role = await Context.Guild.GetRoleAsync(config.ArenaRoleId);

        if (guildUser.RoleIds.Contains(role.Id))
        {
            await RespondAsync($"You are already in the arena", ephemeral: true);
            return;
        }

        var db = new BotContext();
        var currentPoints = await db.GetPointsAsync(Context.User.Id);
        if (currentPoints < config.ArenaEntranceCost)
        {
            await RespondAsync($"It costs **{config.ArenaEntranceCost} points** to buy into the arena, but you only have **{currentPoints} points**.", ephemeral: true);
            return;
        }

        await RespondAsync($"It costs **{config.ArenaEntranceCost} points** to buy into the arena. Are you sure you want to continue?", components: new ComponentBuilder().WithButton("Yes", "confirm_enter_arena", ButtonStyle.Primary).Build(), ephemeral: true);
        var interaction = await InteractionUtility.WaitForComponentInteractionAsync(client, "confirm_enter_arena", Context.Interaction, Context.User, TimeSpan.FromSeconds(10));
        if (interaction != null)
        {
            var newBalance = await db.RemovePointsAsync(Context.Interaction.Id, Context.User.Id, config.ArenaEntranceCost);


            await guildUser.AddRoleAsync(role);

            await ModifyOriginalResponseAsync((properties) =>
            {
                properties.Content = $"You are now registered to enter the arena. Your balance is now **{newBalance} points**.";
                properties.Components = MessageComponent.Empty;
            });
            await ledger.LogTransactionAsync($"{Context.User.Mention} (**{newBalance} points**) spent **{config.ArenaEntranceCost} points** to enter the arena", TransactionType.Delete, Context.Interaction.Id);
        }
        else
        {
            await ModifyOriginalResponseAsync((properties) =>
            {
                properties.Content = "Request to enter the arena timed out.";
                properties.Components = MessageComponent.Empty;
            });
        }
    }
}