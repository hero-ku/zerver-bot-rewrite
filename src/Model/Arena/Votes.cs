using System.ComponentModel.DataAnnotations;

public abstract class Vote
{
    [Key]
    public ulong VoterId { get; set; }
    public ulong TargetId { get; set; }
}

public class ElectionVote : Vote { }
public class OstracismVote : Vote { }