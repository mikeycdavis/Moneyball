namespace Moneyball.Domain.NBA
{
    public class NbaPrediction
    {
        public int GameId { get; set; }
        public string ModelVersion { get; set; } = "v1";
        public string HomeOrAway { get; set; } = string.Empty;
        public float WinProbability { get; set; }
        public float Edge { get; set; }
        public string Confidence { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
    }

}
