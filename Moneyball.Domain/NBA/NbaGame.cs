namespace Moneyball.Domain.NBA
{
    public class NbaGame
    {
        public int Id { get; set; }
        public DateOnly GameDate { get; set; }

        public int HomeTeamId { get; set; }
        public int AwayTeamId { get; set; }

        public int? HomeScore { get; set; }
        public int? AwayScore { get; set; }

        public NbaTeam HomeTeam { get; set; } = null!;
        public NbaTeam AwayTeam { get; set; } = null!;
    }
}
