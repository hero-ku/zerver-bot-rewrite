using System.Text.RegularExpressions;
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

    [Group("pause", "Pauses or resumes an event")]
    public class PauseCommands(ArenaService arenaService) : InteractionModuleBase
    {
        [SlashCommand("arena", "Pause or resumes the arena")]
        public async Task PauseArena()
        {
            var isNowPaused = arenaService.PauseOrResumeEvent();
            await RespondAsync(isNowPaused ? "Paused Arena." : "Resumed Arena.");
        }
    }
}