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
        public decimal CalculateOptimalStake(decimal winProbability, decimal odds, decimal fractionOfKelly = 0.25m)
        {
            // Kelly Criterion: f = (bp - q) / b
            // where b = odds, p = win probability, q = lose probability

            decimal b = odds;
            decimal p = winProbability;
            decimal q = 1 - p;

            decimal kellyFraction = (b * p - q) / b;

            // Apply fractional Kelly
            return Math.Max(0, kellyFraction * fractionOfKelly);
        }
    }
}
