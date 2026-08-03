using Discord;
using Discord.Interactions;
using Discord.WebSocket;

public class Arena(DiscordSocketClient client, BotConfig config, Ledger ledger) : InteractionModuleBase
{
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
            await RespondAsync($"It costs **{config.ArenaEntranceCost} points** to buy into the arena, but you only have **{currentPoints} points**.");
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