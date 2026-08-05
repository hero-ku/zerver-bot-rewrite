using Discord.Interactions;
using Discord.WebSocket;

namespace ZerverBot.Commands;

public class EventCommands : InteractionModuleBase
{
    [Group("start", "Starts an event")]
    public class StartCommands(ArenaService arenaService) : InteractionModuleBase
    {
        [SlashCommand("arena", "Starts the arena")]
        public async Task StartArena()
        {
            await arenaService.StartEvent();
            await RespondAsync("Arena started.");
        }
    }
}