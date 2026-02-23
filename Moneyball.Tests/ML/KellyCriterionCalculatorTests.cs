using FluentAssertions;
using Moneyball.Infrastructure.ML;

namespace Moneyball.Tests.ML
{
    public class KellyCriterionCalculatorTests
    {
        // Note: KellyCriterionCalculator has no dependencies to mock since it's a pure static utility class.
        // Moq would be used if this class had interfaces/services injected, but the Kelly formula
        // is self-contained math — so these are pure unit tests focused on input/output behaviour.

        [Fact]
        public void CalculateOptimalStake_WithFavourableOddsAndHighWinProbability_ReturnsPositiveStake()
        {
            // Arrange
            const decimal winProbability = 0.6m;  // 60% chance of winning
            const decimal odds = 2.0m;            // Evens (2.0 decimal odds)

            // Act
            var result = KellyCriterionCalculator.CalculateOptimalStake(winProbability, odds);

            // Assert
            // Kelly = (2.0 * 0.6 - 0.4) / 2.0 = (1.2 - 0.4) / 2.0 = 0.4
            // Fractional Kelly = 0.4 * 0.25 = 0.1
            result.Should().Be(0.1m);
        }

        [Fact]
        public void CalculateOptimalStake_WithNegativeExpectedValue_ReturnsZero()
        {
            // Arrange — if the Kelly fraction is negative, we should never bet
            const decimal winProbability = 0.3m;  // Only 30% chance of winning
            const decimal odds = 1.5m;            // Low odds

            // Act
            var result = KellyCriterionCalculator.CalculateOptimalStake(winProbability, odds);

            // Assert
            // Kelly = (1.5 * 0.3 - 0.7) / 1.5 = (0.45 - 0.7) / 1.5 = -0.1667 → clamped to 0
            result.Should().Be(0m, "a negative Kelly fraction means no edge, so stake should be zero");
        }

        [Fact]
        public void CalculateOptimalStake_WithBreakEvenExpectedValue_ReturnsZero()
        {
            // Arrange — edge case where Kelly fraction is exactly zero
            const decimal winProbability = 0.4m;
            const decimal odds = 1.5m; // b*p = 1.5*0.4 = 0.6 = q → Kelly = 0

            // Act
            var result = KellyCriterionCalculator.CalculateOptimalStake(winProbability, odds);

            // Assert
            result.Should().Be(0m, "when expected value is exactly zero, the optimal stake is zero");
        }

        [Fact]
        public void CalculateOptimalStake_UsesDefaultQuarterKellyFraction_WhenNotSpecified()
        {
            // Arrange
            const decimal winProbability = 0.6m;
            const decimal odds = 2.0m;

            // Act — call without providing fractionOfKelly to confirm default is 0.25
            var resultDefault = KellyCriterionCalculator.CalculateOptimalStake(winProbability, odds);
            var resultExplicit = KellyCriterionCalculator.CalculateOptimalStake(winProbability, odds);

            // Assert
            resultDefault.Should().Be(resultExplicit, "the default fraction should be 0.25 (quarter Kelly)");
        }

        [Fact]
        public void CalculateOptimalStake_WithFullKelly_ReturnsLargerStakeThanFractionalKelly()
        {
            // Arrange
            const decimal winProbability = 0.6m;
            const decimal odds = 2.0m;

            // Act
            var fullKelly = KellyCriterionCalculator.CalculateOptimalStake(winProbability, odds, 1.0m);
            var quarterKelly = KellyCriterionCalculator.CalculateOptimalStake(winProbability, odds);

            // Assert
            fullKelly.Should().BeGreaterThan(quarterKelly,
                "a higher Kelly fraction should always produce a larger recommended stake");
        }

        [Fact]
        public void CalculateOptimalStake_WithVeryHighWinProbabilityAndOdds_ReturnsLargeButCappedFraction()
        {
            // Arrange — very favourable bet
            const decimal winProbability = 0.9m;
            const decimal odds = 5.0m;

            // Act
            var result = KellyCriterionCalculator.CalculateOptimalStake(winProbability, odds);

            // Assert
            // Kelly = (5*0.9 - 0.1) / 5 = (4.5 - 0.1) / 5 = 4.4 / 5 = 0.88
            // Quarter Kelly = 0.88 * 0.25 = 0.22
            result.Should().Be(0.22m);
            result.Should().BeGreaterThan(0m).And.BeLessThanOrEqualTo(1m,
                "stake fraction should remain a sensible proportion of bankroll");
        }

        [Fact]
        public void CalculateOptimalStake_WithZeroFractionOfKelly_ReturnsZero()
        {
            // Arrange — deliberately passing 0 as the Kelly multiplier
            const decimal winProbability = 0.7m;
            const decimal odds = 3.0m;
            const decimal fractionOfKelly = 0.0m;

            // Act
            var result = KellyCriterionCalculator.CalculateOptimalStake(winProbability, odds, fractionOfKelly);

            // Assert
            result.Should().Be(0m, "multiplying by zero Kelly fraction should always yield a zero stake");
        }
    }
}