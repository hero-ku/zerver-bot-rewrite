using Discord.WebSocket;

public class Ledger(DiscordSocketClient client, BotConfig config)
{
    private readonly SocketTextChannel channel = client.GetGuild(config.GuildId).GetTextChannel(config.LedgerChannelId);

    public async Task LogTransactionAsync(string message, TransactionType type, ulong interactionId)
    {
        await channel.SendMessageAsync($"[*{type.ToString().ToUpper()}*] {message} [{interactionId}]");
    }
}

public enum TransactionType
{
    Create,
    Delete,
    Send,
}