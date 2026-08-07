using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using ZerverBot.Model;

namespace ZerverBot.Commands;

public class InviteCommands(BotConfig config) : InteractionModuleBase
{
    [SlashCommand("invite", "Gets your personal invite link")]
    public async Task GetInvite()
    {
        var db = new BotContext();
        var inviteId = await db.Invites.Where(i => i.UserId == Context.User.Id).Select(i => i.InviteId).SingleOrDefaultAsync();
        if (inviteId is null)
        {
            var channel = await Context.Guild.GetTextChannelAsync(config.InviteChannelId);
            var invite = await channel.CreateInviteAsync(isUnique: true, maxAge: null);
            inviteId = invite.Code;

            db.Add(new Invite(Context.User.Id, inviteId));
            await db.SaveChangesAsync();
        }

        await RespondAsync($"https://discord.gg/{inviteId}", ephemeral: true);
    }

    [SlashCommand("get-user-invite", "Gets a user's invite link")]
    public async Task GetUserInvite(IUser user)
    {
        var db = new BotContext();
        var inviteId = await db.Invites.Where(i => i.UserId == user.Id).Select(i => i.InviteId).SingleOrDefaultAsync();

        await RespondAsync(inviteId is not null ? $"https://discord.gg/{inviteId}" : "An invite has not been created for that user.", ephemeral: true);
    }
}