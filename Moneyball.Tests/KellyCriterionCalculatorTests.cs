using Moneyball.Infrastructure.ML;
using Shouldly;

namespace Moneyball.Tests
{
    public class KellyCriterionCalculatorTests
    {
        //[Theory]
        //[InlineData(0.55, 2.0, 0.25, 0.0125)] // 55% win prob, +100 odds
        //[InlineData(0.60, 1.5, 0.25, 0.05)]
        //public void CalculateOptimalStake_ReturnsCorrectFraction(
        //    decimal winProb, decimal odds, decimal fraction, decimal expected)
        //{
        //    var calculator = new KellyCriterionCalculator();
        //    var result = calculator.CalculateOptimalStake(winProb, odds, fraction);
        //    result.ShouldBe(expected, 4);
        //    Assert.Equal(expected, result, 4);
        //}
    }
}
