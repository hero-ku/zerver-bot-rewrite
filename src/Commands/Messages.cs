using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace ZerverBot.Commands;

public class MessageCommands(DiscordSocketClient client, AdminLog adminLog, BotConfig config) : InteractionModuleBase
{
    [SlashCommand("pm", "Invites another user to start or join a private conversation")]
    public async Task SendMessage(IGuildUser userToMessage, [MaxLength(100)] string? threadName = null)
    {
        var dmChannel = await userToMessage.CreateDMChannelAsync();

        var inviteMessage = await dmChannel.SendMessageAsync(
            embed: new EmbedBuilder()
                .WithTitle("Thread Invite")
                .WithThumbnailUrl("https://img.icons8.com/?size=40&id=86862&format=png&color=FFFFFF")
                .WithDescription($"{Context.User.Mention} is inviting you to join a private conversation. Would you like to accept?")
                .WithFooter(new EmbedFooterBuilder().WithText($"This invite will expire in 60 seconds.").WithIconUrl(Context.User.GetAvatarUrl()))
                .Build(),
            components: new ComponentBuilder()
                .WithButton("Accept", "accept_message", ButtonStyle.Success).Build()
        );
        await RespondAsync("Invite sent.", ephemeral: true);

        if (Context.Channel is IThreadChannel threadChannel)
        {
            await adminLog.LogAsync($"{Context.User.Mention} sent {userToMessage.Mention} an invite to {threadChannel.Mention}.");
        }
        else
        {
            await adminLog.LogAsync($"{Context.User.Mention} sent {userToMessage.Mention} an invite to start a thread called `{threadName}`.");
        }


        var interaction = await InteractionUtility.WaitForComponentInteractionAsync(client, "accept_message", inviteMessage, userToMessage, TimeSpan.FromSeconds(60));
        if (interaction != null)
        {
            IThreadChannel thread;
            if (Context.Channel is IThreadChannel _threadChannel)
            {
                thread = _threadChannel;
            }
            else
            {
                var channel = await Context.Guild.GetTextChannelAsync(config.MessageChannelId);
                thread = await channel.CreateThreadAsync(threadName ?? $"{Context.User.Username}, {userToMessage.Username}", type: ThreadType.PrivateThread, autoArchiveDuration: ThreadArchiveDuration.OneHour, invitable: false);
                await thread.AddUserAsync(await Context.Guild.GetUserAsync(Context.User.Id));
            }

            await thread.AddUserAsync(userToMessage);
            await interaction.DeferAsync();
            await inviteMessage.ModifyAsync((properties) =>
            {
                properties.Content = $"Invite accepted. Click {thread.Mention} to jump to the thread.";
                properties.Components = null;
                properties.Embed = null;
            });
        }
        else
        {
            await inviteMessage.ModifyAsync((properties) =>
            {
                properties.Content = $"Invite timed out. Ask {Context.User.Mention} to send you another.";
                properties.Components = null;
                properties.Embed = null;
            });
        }
    }
}