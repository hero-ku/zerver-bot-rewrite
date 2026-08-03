using System.Data;
using System.Text;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

public class Points(DiscordSocketClient client, Ledger ledger) : InteractionModuleBase
{
    [SlashCommand("points", "Displays your balance of points")]
    public async Task CheckPoints()
    {
        var db = new BotContext();
        var points = await db.GetPointsAsync(Context.User.Id);
        await RespondAsync($"You have **{points} points**.", ephemeral: true);
    }

    [SlashCommand("getpoints", "Displays the point balance of a user")]
    public async Task GetPoints(IUser user)
    {
        var db = new BotContext();
        var points = await db.GetPointsAsync(user.Id);
        await RespondAsync($"{user.Mention} has **{points} points**.", ephemeral: true);
    }

    [SlashCommand("addpoints", "Adds points to a user's balance")]
    public async Task AddPoints(IUser user, uint amount)
    {
        var db = new BotContext();
        var points = await db.AddPointsAsync(Context.Interaction.Id, user.Id, amount);
        await RespondAsync($"Successfully gave **{amount} points** to {user.Mention} (**{points} points**).", ephemeral: true);
        await ledger.LogTransactionAsync($"{Context.User.Mention} gave **{amount} points** to {user.Mention} (**{points} points**)", TransactionType.Create, Context.Interaction.Id);
    }

    [SlashCommand("removepoints", "Removes points from a user's balance")]
    public async Task RemovePoints(IUser user, uint amount)
    {
        var db = new BotContext();
        var points = await db.RemovePointsAsync(Context.Interaction.Id, user.Id, amount);
        await RespondAsync($"Successfully removed **{amount} points** from {user.Mention} (**{points} points**).", ephemeral: true);
        await ledger.LogTransactionAsync($"{Context.User.Mention} removed **{amount} points** from {user.Mention} (**{points} points**)", TransactionType.Delete, Context.Interaction.Id);
    }

    [SlashCommand("zelle", "Send points to another user")]
    public async Task SendPoints(IUser recipient, uint amount)
    {
        var db = new BotContext();

        var authorRecord = await db.GetUserAsync(Context.User.Id);
        if (amount > authorRecord.Points)
        {
            await FollowupAsync($"You only have **{authorRecord.Points} points**.");
            return;
        }

        await RespondAsync($"Are you sure you want to send **{amount} points** to {recipient.Mention}?", components: new ComponentBuilder().WithButton("Yes", "confirm_send_points", ButtonStyle.Primary).Build(), ephemeral: true);
        var interaction = await InteractionUtility.WaitForComponentInteractionAsync(client, "confirm_send_points", Context.Interaction, Context.User, TimeSpan.FromSeconds(10));
        if (interaction != null)
        {
            var (newAuthorBalance, newRecipientBalance) = await db.TransferPointsAsync(Context.Interaction.Id, Context.User.Id, recipient.Id, amount);
            await ModifyOriginalResponseAsync((properties) =>
            {
                properties.Content = $"You sent {recipient.Mention} **{amount} points**.";
                properties.Components = MessageComponent.Empty;
            });
            await ledger.LogTransactionAsync($"{Context.User.Mention} (**{newAuthorBalance} points**) sent **{amount} points** to {recipient.Mention} (**{newRecipientBalance} points**)", TransactionType.Send, Context.Interaction.Id);
        }
        else
        {
            await ModifyOriginalResponseAsync((properties) =>
            {
                properties.Content = "Request to send points timed out!";
                properties.Components = MessageComponent.Empty;
            });
        }
    }

    [SlashCommand("ledger", "Formats the transaction history as a CSV file")]
    public async Task GetLedger()
    {
        var db = new BotContext();
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);

        writer.WriteLine("ID,Sender,Recipient,Amount,Time");
        await foreach (var transaction in db.Transactions.OrderDescending().AsAsyncEnumerable())
        {
            await writer.WriteLineAsync($"{transaction.InteractionId},{transaction.SenderId},{transaction.RecipientId},{transaction.Amount},{transaction.Timestamp.DateTime}");
        }
        await writer.FlushAsync();
        stream.Position = 0;

        var fileAttachment = new FileAttachment(stream, "ledger.csv");
        await RespondWithFileAsync(fileAttachment);
    }
}