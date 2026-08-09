namespace ZerverBot;

public class BotConfig
{
    public ulong GuildId { set; get; }
    public ulong LedgerChannelId { set; get; }
    public ulong SpeakerChannelId { set; get; }
    public ulong InviteChannelId { set; get; }
    public ulong CommonsCategoryId { set; get; }

    public ulong MessageChannelId { set; get; }

    public ulong ArenaRoleId { set; get; }
    public ulong ArenaParticipantRoleId { set; get; }
    public ulong ArenaChannelId { set; get; }
    public uint ArenaEntranceCost { set; get; }
    public int OstracismPeriod { set; get; }
    public int ElectionTimeOfDay { set; get; }

    public uint OstracismVotingReward { set; get; }
    public uint ElectionVotingReward { set; get; }
}