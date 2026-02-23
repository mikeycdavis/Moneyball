namespace Moneyball.Infrastructure.ML
{
    public class KellyCriterionCalculator
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="winProbability"></param>
        /// <param name="odds"></param>
        /// <param name="fractionOfKelly">Fractional Kelly for safety</param>
        /// <returns></returns>
        public static decimal CalculateOptimalStake(decimal winProbability, decimal odds, decimal fractionOfKelly = 0.25m)
        {
            // Kelly Criterion: f = (bp - q) / b
            // where b = odds, p = win probability, q = lose probability

            var b = odds;
            var p = winProbability;
            var q = 1 - p;

            var kellyFraction = (b * p - q) / b;

            // Apply fractional Kelly
            return Math.Max(0, kellyFraction * fractionOfKelly);
        }
    }
}
