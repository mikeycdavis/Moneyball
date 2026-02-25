"""
Edge and Expected Value (EV) calculator.

Computes edge and EV for betting decisions by comparing
model probabilities against bookmaker implied probabilities.

Acceptance Criteria:
- Model probability compared against implied bookmaker probability
- Edge calculated as: model_probability - implied_probability
- Expected value (EV) calculated based on odds and model probability
- Returns recommended side if edge > threshold
"""

import logging
from typing import Dict, Any, Optional
from dataclasses import dataclass

logger = logging.getLogger(__name__)


@dataclass
class EdgeCalculation:
    """
    Result of edge calculation.
    
    Acceptance Criteria: Response returns:
    - Model probability
    - Implied probability
    - Edge
    - EV
    - Recommended side (if edge > threshold)
    """
    model_probability: float
    implied_probability: float
    edge: float
    expected_value: float
    recommended_side: Optional[str]
    confidence: float
    
    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for API response."""
        return {
            'model_probability': round(self.model_probability, 4),
            'implied_probability': round(self.implied_probability, 4),
            'edge': round(self.edge, 4),
            'expected_value': round(self.expected_value, 4),
            'recommended_side': self.recommended_side,
            'confidence': round(self.confidence, 4)
        }


def calculate_edge(
    model_probability: float,
    bookmaker_odds: float,
    odds_format: str = "american",
    edge_threshold: float = 0.05,
    bet_amount: float = 100.0
) -> EdgeCalculation:
    """
    Calculate edge and expected value for a betting opportunity.
    
    Acceptance Criteria:
    - Edge = model_probability - implied_probability
    - EV calculated based on odds and model probability
    - Recommended side if edge > threshold
    
    Args:
        model_probability: Probability from ML model (0 to 1)
        bookmaker_odds: Bookmaker odds
        odds_format: Format of odds ("american", "decimal", "fractional")
        edge_threshold: Minimum edge to recommend bet (default 0.05 = 5%)
        bet_amount: Bet size for EV calculation (default $100)
        
    Returns:
        EdgeCalculation with all computed values
        
    Example:
        >>> calc = calculate_edge(
        ...     model_probability=0.60,
        ...     bookmaker_odds=-110,  # American odds
        ...     edge_threshold=0.05
        ... )
        >>> calc.edge  # 0.60 - 0.524 = 0.076 (7.6% edge)
        0.076
        >>> calc.recommended_side  # "bet" (edge > 5%)
        'bet'
    """
    logger.debug(
        f"Calculating edge: model_prob={model_probability}, "
        f"odds={bookmaker_odds}, format={odds_format}"
    )
    
    # Step 1: Convert bookmaker odds to implied probability
    # Acceptance Criteria: Converts odds to implied probabilities
    implied_probability = odds_to_implied_probability(bookmaker_odds, odds_format)
    
    # Step 2: Calculate edge
    # Acceptance Criteria: Edge = model_probability - implied_probability
    edge = model_probability - implied_probability
    
    # Step 3: Calculate expected value (EV)
    # Acceptance Criteria: EV calculated based on odds and model probability
    ev = calculate_expected_value(
        model_probability,
        bookmaker_odds,
        odds_format,
        bet_amount
    )
    
    # Step 4: Determine recommendation
    # Acceptance Criteria: Recommended side if edge > threshold
    if edge > edge_threshold:
        recommended_side = "bet"
    elif edge < -edge_threshold:
        recommended_side = "pass"
    else:
        recommended_side = "neutral"
    
    # Calculate confidence (absolute edge as confidence indicator)
    confidence = abs(edge) * 2  # Scale to 0-1 range
    confidence = min(confidence, 1.0)
    
    result = EdgeCalculation(
        model_probability=model_probability,
        implied_probability=implied_probability,
        edge=edge,
        expected_value=ev,
        recommended_side=recommended_side,
        confidence=confidence
    )
    
    logger.debug(
        f"Edge calculation complete: edge={edge:.4f}, "
        f"ev=${ev:.2f}, recommendation={recommended_side}"
    )
    
    return result


def odds_to_implied_probability(
    odds: float,
    odds_format: str = "american"
) -> float:
    """
    Convert bookmaker odds to implied probability.
    
    Handles different odds formats:
    - American: -110, +150, etc.
    - Decimal: 1.91, 2.50, etc.
    - Fractional: 5/2, 11/10, etc. (as string)
    
    Args:
        odds: Bookmaker odds value
        odds_format: Format ("american", "decimal", "fractional")
        
    Returns:
        Implied probability (0 to 1)
    """
    if odds_format == "american":
        if odds > 0:
            # Positive odds: probability = 100 / (odds + 100)
            probability = 100 / (odds + 100)
        else:
            # Negative odds: probability = abs(odds) / (abs(odds) + 100)
            probability = abs(odds) / (abs(odds) + 100)
    
    elif odds_format == "decimal":
        # Decimal odds: probability = 1 / odds
        probability = 1 / odds
    
    elif odds_format == "fractional":
        # Fractional odds: "5/2" -> probability = denominator / (numerator + denominator)
        if isinstance(odds, str):
            numerator, denominator = map(float, odds.split('/'))
            probability = denominator / (numerator + denominator)
        else:
            raise ValueError("Fractional odds must be string in format 'numerator/denominator'")
    
    else:
        raise ValueError(f"Unsupported odds format: {odds_format}")
    
    return probability


def calculate_expected_value(
    model_probability: float,
    bookmaker_odds: float,
    odds_format: str = "american",
    bet_amount: float = 100.0
) -> float:
    """
    Calculate expected value (EV) of a bet.
    
    EV = (probability_of_win * profit_if_win) - (probability_of_loss * loss_if_loss)
    
    Args:
        model_probability: Probability from model (0 to 1)
        bookmaker_odds: Bookmaker odds
        odds_format: Format of odds
        bet_amount: Bet size
        
    Returns:
        Expected value in currency units
    """
    # Calculate payout from odds
    payout = odds_to_payout(bookmaker_odds, odds_format, bet_amount)
    
    # Profit if win (payout - original bet)
    profit_if_win = payout - bet_amount
    
    # Loss if lose (lose the bet amount)
    loss_if_loss = bet_amount
    
    # Calculate EV
    ev = (model_probability * profit_if_win) - ((1 - model_probability) * loss_if_loss)
    
    return ev


def odds_to_payout(
    odds: float,
    odds_format: str = "american",
    bet_amount: float = 100.0
) -> float:
    """
    Calculate total payout (including original stake) from odds.
    
    Args:
        odds: Bookmaker odds
        odds_format: Format of odds
        bet_amount: Bet size
        
    Returns:
        Total payout including original stake
    """
    if odds_format == "american":
        if odds > 0:
            # Positive odds: payout = stake + (stake * odds / 100)
            return bet_amount + (bet_amount * odds / 100)
        else:
            # Negative odds: payout = stake + (stake * 100 / abs(odds))
            return bet_amount + (bet_amount * 100 / abs(odds))
    
    elif odds_format == "decimal":
        # Decimal odds: payout = stake * odds
        return bet_amount * odds
    
    elif odds_format == "fractional":
        # Fractional odds: "5/2" -> payout = stake + (stake * 5/2)
        if isinstance(odds, str):
            numerator, denominator = map(float, odds.split('/'))
            return bet_amount + (bet_amount * numerator / denominator)
        else:
            raise ValueError("Fractional odds must be string")
    
    else:
        raise ValueError(f"Unsupported odds format: {odds_format}")


def calculate_kelly_criterion(
    model_probability: float,
    bookmaker_odds: float,
    odds_format: str = "american",
    fraction: float = 0.25
) -> float:
    """
    Calculate optimal bet size using Kelly Criterion.
    
    Kelly % = (bp - q) / b
    where:
    - b = decimal odds - 1 (net odds)
    - p = probability of winning
    - q = probability of losing (1 - p)
    
    Args:
        model_probability: Probability from model
        bookmaker_odds: Bookmaker odds
        odds_format: Format of odds
        fraction: Fractional Kelly (0.25 = quarter Kelly for safety)
        
    Returns:
        Recommended bet size as fraction of bankroll (0 to 1)
    """
    # Convert to decimal odds
    if odds_format == "american":
        if bookmaker_odds > 0:
            decimal_odds = (bookmaker_odds / 100) + 1
        else:
            decimal_odds = (100 / abs(bookmaker_odds)) + 1
    elif odds_format == "decimal":
        decimal_odds = bookmaker_odds
    else:
        raise ValueError("Kelly criterion requires american or decimal odds")
    
    # Calculate Kelly percentage
    b = decimal_odds - 1  # Net odds
    p = model_probability
    q = 1 - p
    
    kelly_pct = (b * p - q) / b
    
    # Apply fractional Kelly for safety
    kelly_pct = kelly_pct * fraction
    
    # Ensure non-negative
    kelly_pct = max(0, kelly_pct)
    
    return kelly_pct