using System.Diagnostics.CodeAnalysis;
using System.Text;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using ZerverBot;
using ZerverBot.Model;
using ZerverBot.Model.Arena;

namespace ZerverBot.Commands;

public class ArenaCommands(DiscordSocketClient client, ArenaService arenaService, BotConfig config, Ledger ledger) : InteractionModuleBase
{
    [SlashCommand("holdvote", "Hold a vote manually")]
    public async Task HoldOstracism()
    {
        await arenaService.HoldOstracism();
    }

    [SlashCommand("holdelection", "Hold a vote manually")]
    public async Task HoldElection()
    {
        await arenaService.HoldElection();
    }

    [SlashCommand("ostracize", "Vote to ostracize a member")]
    public async Task Ostracize(IGuildUser target)
    {
        if (!target.RoleIds.Contains(config.ArenaRoleId))
        {
            await RespondAsync($"{target.Mention} is not in the arena.", ephemeral: true);
        }

        var db = new BotContext();
        var vote = await db.OstracismVotes.Where(v => v.VoterId == Context.User.Id).SingleOrDefaultAsync();
        if (vote == null)
        {
            vote = new OstracismVote { VoterId = Context.User.Id };
            db.Add(vote);
        }

        vote.TargetId = target.Id;
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
        var vote = await db.ElectionVotes.Where(v => v.VoterId == Context.User.Id).SingleOrDefaultAsync();
        if (vote == null)
        {
            vote = new ElectionVote { VoterId = Context.User.Id };
            db.Add(vote);
        }

        vote.TargetId = target.Id;
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
                ? await ArenaService.GetUserVotes(db.OstracismVotes, Context.Guild)
                : await ArenaService.GetVoteCounts(db.OstracismVotes, Context.Guild)
            );
        }

        [SlashCommand("election", "Gets the current of state of the election")]
        public async Task GetElectionVotes(bool listUserVotes = false)
        {
            var db = new BotContext();
            await RespondAsync(listUserVotes
                ? await ArenaService.GetUserVotes(db.ElectionVotes, Context.Guild)
                : await ArenaService.GetVoteCounts(db.ElectionVotes, Context.Guild)
            );
        }
    }

    [SlashCommand("enter", "The arena awaits...")]
    public async Task EnterArena()
    {
        var guildUser = await Context.Guild.GetUserAsync(Context.User.Id);
        var role = await Context.Guild.GetRoleAsync(config.ArenaRoleId);

        if (guildUser.RoleIds.Contains(role.Id))
        {
            await RespondAsync($"You are already in the arena.", ephemeral: true);
            return;
        }

        var db = new BotContext();
        var currentPoints = await db.GetPointsAsync(Context.User.Id);
        if (currentPoints < config.ArenaEntranceCost)
        {
            await RespondAsync($"It costs **{config.ArenaEntranceCost:N0} points** to buy into the arena, but you only have **{currentPoints:N0} points**.", ephemeral: true);
            return;
        }

        await RespondAsync($"It costs **{config.ArenaEntranceCost:N0} points** to buy into the arena. Are you sure you want to continue?", components: new ComponentBuilder().WithButton("Yes", "confirm_enter_arena", ButtonStyle.Primary).Build(), ephemeral: true);
        var interaction = await InteractionUtility.WaitForComponentInteractionAsync(client, "confirm_enter_arena", Context.Interaction, Context.User, TimeSpan.FromSeconds(10));
        if (interaction != null)
        {
            var newBalance = await db.RemovePointsAsync(Context.Interaction.Id, Context.User.Id, config.ArenaEntranceCost);


            await guildUser.AddRoleAsync(role);

            await ModifyOriginalResponseAsync((properties) =>
            {
                properties.Content = $"You are now registered to enter the arena. Your balance is now **{newBalance:N0} points**.";
                properties.Components = MessageComponent.Empty;
            });
            await ledger.LogTransactionAsync($"{Context.User.Mention} (**{newBalance:N0} points**) spent **{config.ArenaEntranceCost:N0} points** to enter the arena", TransactionType.Delete, Context.Interaction.Id);
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