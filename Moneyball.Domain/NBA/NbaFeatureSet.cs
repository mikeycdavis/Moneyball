namespace Moneyball.Domain.NBA
{
    public class NbaFeatureSet
    {
        public int GameId { get; set; }

        public float EloDiff { get; set; }
        public float RestDiff { get; set; }
        public float BackToBackDiff { get; set; }
        public float InjuryMinutesLostDiff { get; set; }
        public float HomeAdvantage { get; set; }
    }
}
