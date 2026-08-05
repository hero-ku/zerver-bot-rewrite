using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace ZerverBot;

public static class Extensions
{
    extension(InteractionUtility)
    {
        public static async Task<SocketInteraction?> WaitForComponentInteractionAsync(BaseSocketClient client, string customId, IDiscordInteraction fromInteraction, IUser user, TimeSpan timeout)
        {
            return await InteractionUtility.WaitForInteractionAsync(client, timeout, interaction => interaction is SocketMessageComponent socketMessageComponent && socketMessageComponent.Data.CustomId == customId && interaction.User == user && socketMessageComponent.Message.Interaction.Id == fromInteraction.Id);
        }
    }

}