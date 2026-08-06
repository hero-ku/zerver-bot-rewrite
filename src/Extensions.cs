using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace ZerverBot;

public static class Extensions
{
    extension(InteractionUtility)
    {
        public static async Task<SocketInteraction> WaitForComponentInteractionAsync(BaseSocketClient client, string customId, IMessage fromMessage, IUser user, TimeSpan timeout)
        {
            return await InteractionUtility.WaitForInteractionAsync(client, timeout, interaction =>
                {
                    return interaction is SocketMessageComponent socketMessageComponent &&
                           socketMessageComponent.Data.CustomId == customId && interaction.User.Id == user.Id &&
                           socketMessageComponent.Message.Id == fromMessage.Id;
                }
            );
        }

        public static async Task<SocketInteraction> WaitForComponentInteractionAsync(BaseSocketClient client, string customId, IDiscordInteraction fromInteraction, IUser user, TimeSpan timeout)
        {
            return await InteractionUtility.WaitForInteractionAsync(client, timeout, interaction =>
                {
                    return interaction is SocketMessageComponent socketMessageComponent &&
                           socketMessageComponent.Data.CustomId == customId && interaction.User.Id == user.Id &&
                           socketMessageComponent.Message.Interaction.Id == fromInteraction.Id;
                }
            );
        }
    }

}