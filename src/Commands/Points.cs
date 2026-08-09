using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using ZerverBot.Model;

namespace ZerverBot.Commands;

public class PointCommands(DiscordSocketClient client, Ledger ledger) : InteractionModuleBase
{
    [SlashCommand("balance", "Checks your balance of points")]
    public async Task CheckPoints()
    {
        var db = new BotContext();
        var points = await db.GetPointsAsync(Context.User.Id);
        await RespondAsync($"You have **{points:N0} points**.", ephemeral: true);
    }

    [SlashCommand("zelle", "Send points to another user")]
    public async Task SendPoints(IUser recipient, uint amount)
    {
        var db = new BotContext();

        var points = await db.GetPointsAsync(Context.User.Id);
        if (amount > points)
        {
            await FollowupAsync($"You only have **{points:N0} points**.");
            return;
        }

        await RespondAsync($"Are you sure you want to send **{amount:N0} points** to {recipient.Mention}?", components: new ComponentBuilder().WithButton("Yes", "confirm_send_points", ButtonStyle.Primary).Build(), ephemeral: true);
        var interaction = await InteractionUtility.WaitForComponentInteractionAsync(client, "confirm_send_points", Context.Interaction, Context.User, TimeSpan.FromSeconds(10));
        if (interaction != null)
        {
            var (newAuthorBalance, newRecipientBalance) = await db.TransferPointsAsync(Context.Interaction.Id, Context.User.Id, recipient.Id, amount);
            await ModifyOriginalResponseAsync((properties) =>
            {
                properties.Content = $"You sent {recipient.Mention} **{amount:N0} points**.";
                properties.Components = MessageComponent.Empty;
            });
            await ledger.LogTransactionAsync($"{Context.User.Mention} (**{newAuthorBalance:N0} points**) sent **{amount:N0} points** to {recipient.Mention} (**{newRecipientBalance:N0} points**)", TransactionType.Send, Context.Interaction.Id);
        }
        else
        {
            await ModifyOriginalResponseAsync((properties) =>
            {
                properties.Content = "Request to send points timed out!";
                properties.Components = MessageComponent.Empty;
            });
        }

        var dmChannel = await recipient.CreateDMChannelAsync();
        await dmChannel.SendMessageAsync($"{Context.User.Mention} sent you **{amount:N0} points**!");
    }

    [Group("points", "Manage points")]
    public class PointAdminCommands(Ledger ledger) : InteractionModuleBase
    {
        [SlashCommand("get", "Displays the point balance of a user")]
        public async Task GetPoints(IUser user)
        {
            var db = new BotContext();
            var points = await db.GetPointsAsync(user.Id);
            await RespondAsync($"{user.Mention} has **{points:N0} points**.", ephemeral: true);
        }

        [SlashCommand("add", "Adds points to a user's balance")]
        public async Task AddPoints(IUser user, uint amount, string reason)
        {
            var db = new BotContext();
            var points = await db.AddPointsAsync(Context.Interaction.Id, user.Id, amount);
            await RespondAsync($"Successfully gave **{amount:N0} points** to {user.Mention} (**{points:N0} points**).", ephemeral: true);
            await ledger.LogTransactionAsync($"{Context.User.Mention} gave **{amount:N0} points** to {user.Mention} (**{points:N0} points**) for **{reason}**", TransactionType.Create, Context.Interaction.Id);

            var dmChannel = await user.CreateDMChannelAsync();
            await dmChannel.SendMessageAsync($"You have been awarded **{amount:N0} points** for **{reason}**.");
        }

        [SlashCommand("remove", "Removes points from a user's balance")]
        public async Task RemovePoints(IUser user, uint amount)
        {
            var db = new BotContext();
            var points = await db.RemovePointsAsync(Context.Interaction.Id, user.Id, amount);
            await RespondAsync($"Successfully removed **{amount:N0} points** from {user.Mention} (**{points:N0} points**).", ephemeral: true);
            await ledger.LogTransactionAsync($"{Context.User.Mention} removed **{amount:N0} points** from {user.Mention} (**{points:N0} points**)", TransactionType.Delete, Context.Interaction.Id);
        }

        [SlashCommand("ledger", "Formats the transaction history as a CSV file")]
        public async Task GetLedger(TimeZoneName zoneName = TimeZoneName.Utc)
        {
            var db = new BotContext();
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);

            var zoneInfo = CreateTimeZoneInfoFromName(zoneName);

            await writer.WriteLineAsync("ID,Sender,Recipient,Amount,Time");
            await foreach (var transaction in db.Transactions.OrderByDescending(t => t.InteractionId).AsAsyncEnumerable())
            {
                await writer.WriteLineAsync($"{transaction.InteractionId},{transaction.SenderId},{transaction.RecipientId},{transaction.Amount},{TimeZoneInfo.ConvertTime(transaction.Timestamp, zoneInfo).DateTime}");
            }
            await writer.FlushAsync();
            stream.Position = 0;

            var fileAttachment = new FileAttachment(stream, "ledger.csv");
            await RespondWithFileAsync(fileAttachment);
        }

        public enum TimeZoneName
        {
            [ChoiceDisplay("UTC")]
            Utc,
            [ChoiceDisplay("CST")]
            Cst,
            [ChoiceDisplay("PST")]
            Pst,
        }

        private static TimeZoneInfo CreateTimeZoneInfoFromName(TimeZoneName zoneName)
        {
            var id = zoneName switch
            {
                TimeZoneName.Utc => "Etc/UTC",
                TimeZoneName.Cst => "America/Chicago",
                TimeZoneName.Pst => "America/Los_Angeles",
                _ => throw new ArgumentOutOfRangeException(nameof(zoneName))
            };
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
    }

    [Group("reserve", "Manage the point reserve")]
    public class ReserveCommands(Ledger ledger) : InteractionModuleBase
    {
        [SlashCommand("get", "Gets the balance of the reserve")]
        public async Task GetReservePoints()
        {
            var db = new BotContext();
            var points = await db.GetReservePointsAsync();
            await RespondAsync($"The reserve contains **{points:N0} points**.", ephemeral: true);
        }

        [SlashCommand("add", "Adds points to the reserve")]
        public async Task AddReservePoints(uint amount)
        {
            var db = new BotContext();
            var points = await db.AddReservePointsAsync(Context.Interaction.Id, amount);
            await RespondAsync($"Successfully added **{amount:N0} points** to the reserve (**{points:N0} points**).", ephemeral: true);
            await ledger.LogTransactionAsync($"{Context.User.Mention} added **{amount:N0} points** to the reserve (**{points:N0} points**)", TransactionType.Create, Context.Interaction.Id);
        }

        [SlashCommand("pay", "Pay a user points from the reserve")]
        public async Task PayFromReserve(IUser user, uint amount, string reason)
        {
            var db = new BotContext();
            var (newReserveBalance, newUserBalance) = await db.PayFromReserveAsync(Context.Interaction.Id, user.Id, amount);
            await RespondAsync($"Successfully paid **{amount:N0} points** to {user.Mention} (**{newUserBalance:N0}**) from the reserve.", ephemeral: true);
            await ledger.LogTransactionAsync($"{Context.User.Mention} paid **{amount:N0} points** to {user.Mention} (**{newUserBalance:N0}**) from the reserve (**{newReserveBalance:N0} points**) for **{reason}**", TransactionType.Send, Context.Interaction.Id);

            var dmChannel = await user.CreateDMChannelAsync();
            await dmChannel.SendMessageAsync($"You have been awarded **{amount:N0} points** for **{reason}**.");
        }
    }
}