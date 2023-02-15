namespace ForumDataMigration.Models;

public class Poll
{
    [Slapper.AutoMapper.Id]
    public int Tid { get; set; }

    public bool Overt { get; set; }
    public bool Visible { get; set; }
    public byte Maxchoices { get; set; }
    public uint Expiration { get; set; }
    public uint Voters { get; set; }
    public uint Authorid { get; set; }
    public uint Dateline { get; set; }

    public virtual ICollection<PollOption> PollOptions { get; set; } = new HashSet<PollOption>();
    public virtual ICollection<PollVoter> PollVoters { get; set; } = new HashSet<PollVoter>();
}