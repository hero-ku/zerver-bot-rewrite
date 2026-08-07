using System.Text;
using Discord;
using Discord.WebSocket;

namespace ZerverBot;

public class AdminLog(DiscordSocketClient client, BotConfig config)
{
    private readonly SocketTextChannel channel = client.GetGuild(config.GuildId).GetTextChannel(config.SpeakerChannelId);

    public async Task LogAsync(string message)
    {
        await channel.SendMessageAsync(message);
    }

    public async Task AnnounceAsync(string message)
    {
        var builder = new StringBuilder();
        builder.AppendLine("@everyone");
        builder.AppendLine();
        builder.Append(message);

        await channel.SendMessageAsync(builder.ToString());
    }
}