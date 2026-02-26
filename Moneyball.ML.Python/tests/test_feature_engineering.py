"""
Unit tests for feature_engineering.py

Tests all feature engineering functions:

- prepare_features() - Main feature preparation
- add_team_stats_features() - Team statistics
- add_player_stats_features() - Player statistics
- add_betting_lines_features() - Betting market features
- add_derived_features() - Derived features
- add_matchup_features() - Matchup features
- american_odds_to_probability() - Odds conversion
- get_feature_columns() - Feature column extraction

Test Structure:

- Test each feature group addition
- Test acceptance criteria
- Test edge cases
- Test data transformations
"""

import pytest
import pandas as pd
import numpy as np
from typing import List

# Import the module to test

from moneyball_ml_python.training.feature_engineering import (
prepare_features,
add_team_stats_features,
add_player_stats_features,
add_betting_lines_features,
add_derived_features,
add_matchup_features,
american_odds_to_probability,
get_feature_columns
)
from moneyball_ml_python.training.model_config import FeatureGroup

# ====================

# Fixtures

# ====================

@pytest.fixture
def sample_dataframe():
“””
Create a sample DataFrame for testing.

```
Returns:
    pd.DataFrame with minimal game data
"""
return pd.DataFrame({
    'game_id': [1, 2, 3],
    'home_offensive_rating': [115.0, 110.0, 112.0],
    'away_offensive_rating': [108.0, 112.0, 110.0],
    'home_defensive_rating': [105.0, 108.0, 107.0],
    'away_defensive_rating': [110.0, 106.0, 108.0]
})
```

@pytest.fixture
def empty_dataframe():
“””
Create an empty DataFrame for testing feature addition.

```
Returns:
    pd.DataFrame with minimal columns
"""
return pd.DataFrame({
    'game_id': [1, 2, 3, 4, 5]
})
```

# ====================

# Tests for prepare_features()

# ====================

class TestPrepareFeatures:
“””
Tests for main prepare_features() function.

```
Acceptance Criteria: prepare_features(df, model_type)
"""

def test_returns_dataframe(self, sample_dataframe):
    """Test that prepare_features returns a DataFrame."""
    # Act: Prepare features
    result = prepare_features(sample_dataframe, [FeatureGroup.TEAM_STATS])
    
    # Assert: Should return DataFrame
    assert isinstance(result, pd.DataFrame)

def test_adds_team_stats_features(self, empty_dataframe):
    """Test that TEAM_STATS feature group adds features."""
    # Act: Prepare with team stats
    result = prepare_features(empty_dataframe, [FeatureGroup.TEAM_STATS])
    
    # Assert: Should have team stats columns
    assert 'home_offensive_rating' in result.columns
    assert 'away_offensive_rating' in result.columns
    assert 'home_defensive_rating' in result.columns
    assert 'away_defensive_rating' in result.columns

def test_adds_player_stats_features(self, empty_dataframe):
    """Test that PLAYER_STATS feature group adds features."""
    # Act: Prepare with player stats
    result = prepare_features(empty_dataframe, [FeatureGroup.PLAYER_STATS])
    
    # Assert: Should have player stats columns
    assert 'player_points_avg' in result.columns
    assert 'player_rebounds_avg' in result.columns
    assert 'player_usage_rate' in result.columns

def test_adds_betting_lines_features(self, empty_dataframe):
    """Test that BETTING_LINES feature group adds features."""
    # Act: Prepare with betting lines
    result = prepare_features(empty_dataframe, [FeatureGroup.BETTING_LINES])
    
    # Assert: Should have betting lines columns
    assert 'spread_line' in result.columns
    assert 'total_line' in result.columns
    assert 'home_moneyline_odds' in result.columns

def test_adds_derived_features(self, sample_dataframe):
    """Test that DERIVED feature group adds features."""
    # Act: Prepare with derived features
    result = prepare_features(sample_dataframe, [FeatureGroup.DERIVED])
    
    # Assert: Should have derived columns
    assert 'offensive_rating_diff' in result.columns
    assert 'defensive_rating_diff' in result.columns

def test_adds_matchup_features(self, empty_dataframe):
    """Test that MATCHUP feature group adds features."""
    # Act: Prepare with matchup features
    result = prepare_features(empty_dataframe, [FeatureGroup.MATCHUP])
    
    # Assert: Should have matchup columns
    assert 'home_h2h_win_pct' in result.columns

def test_adds_multiple_feature_groups(self, empty_dataframe):
    """Test adding multiple feature groups at once."""
    # Act: Prepare with multiple groups
    result = prepare_features(
        empty_dataframe,
        [FeatureGroup.TEAM_STATS, FeatureGroup.PLAYER_STATS]
    )
    
    # Assert: Should have both types of features
    assert 'home_offensive_rating' in result.columns  # Team stats
    assert 'player_points_avg' in result.columns      # Player stats

def test_does_not_modify_original_dataframe(self, sample_dataframe):
    """Test that original DataFrame is not modified."""
    # Arrange: Get original column count
    original_columns = len(sample_dataframe.columns)
    
    # Act: Prepare features
    result = prepare_features(sample_dataframe, [FeatureGroup.DERIVED])
    
    # Assert: Original should be unchanged
    assert len(sample_dataframe.columns) == original_columns

def test_handles_empty_feature_groups(self, sample_dataframe):
    """Test behavior with empty feature groups list."""
    # Act: Prepare with no feature groups
    result = prepare_features(sample_dataframe, [])
    
    # Assert: Should return copy of original
    assert len(result.columns) == len(sample_dataframe.columns)
```

# ====================

# Tests for add_team_stats_features()

# ====================

class TestAddTeamStatsFeatures:
“””
Tests for team statistics feature addition.

```
Acceptance Criteria: Team-Based Features
- Offensive rating
- Defensive rating
- Pace
- Rebounds
- Turnovers
"""

def test_adds_offensive_ratings(self, empty_dataframe):
    """
    Test that offensive ratings are added.
    
    Acceptance Criteria: Offensive rating
    """
    # Act: Add team stats
    result = add_team_stats_features(empty_dataframe)
    
    # Assert: Should have offensive ratings
    assert 'home_offensive_rating' in result.columns
    assert 'away_offensive_rating' in result.columns

def test_adds_defensive_ratings(self, empty_dataframe):
    """
    Test that defensive ratings are added.
    
    Acceptance Criteria: Defensive rating
    """
    # Act: Add team stats
    result = add_team_stats_features(empty_dataframe)
    
    # Assert: Should have defensive ratings
    assert 'home_defensive_rating' in result.columns
    assert 'away_defensive_rating' in result.columns

def test_adds_pace(self, empty_dataframe):
    """
    Test that pace is added.
    
    Acceptance Criteria: Pace
    """
    # Act: Add team stats
    result = add_team_stats_features(empty_dataframe)
    
    # Assert: Should have pace
    assert 'home_pace' in result.columns
    assert 'away_pace' in result.columns

def test_adds_rebounds(self, empty_dataframe):
    """
    Test that rebounds are added.
    
    Acceptance Criteria: Rebounds
    """
    # Act: Add team stats
    result = add_team_stats_features(empty_dataframe)
    
    # Assert: Should have rebounds
    assert 'home_rebounds_avg' in result.columns
    assert 'away_rebounds_avg' in result.columns

def test_adds_turnovers(self, empty_dataframe):
    """
    Test that turnovers are added.
    
    Acceptance Criteria: Turnovers
    """
    # Act: Add team stats
    result = add_team_stats_features(empty_dataframe)
    
    # Assert: Should have turnovers
    assert 'home_turnovers_avg' in result.columns
    assert 'away_turnovers_avg' in result.columns

def test_preserves_existing_columns(self, sample_dataframe):
    """Test that existing team stats columns are preserved."""
    # Act: Add team stats (already has some)
    result = add_team_stats_features(sample_dataframe)
    
    # Assert: Should keep original values
    assert result['home_offensive_rating'].equals(
        sample_dataframe['home_offensive_rating']
    )

def test_values_in_realistic_ranges(self, empty_dataframe):
    """Test that generated values are in realistic ranges."""
    # Act: Add team stats
    result = add_team_stats_features(empty_dataframe)
    
    # Assert: Check ranges
    assert result['home_offensive_rating'].min() >= 100
    assert result['home_offensive_rating'].max() <= 120
    
    assert result['home_pace'].min() >= 95
    assert result['home_pace'].max() <= 105
```

# ====================

# Tests for add_player_stats_features()

# ====================

class TestAddPlayerStatsFeatures:
“””
Tests for player statistics feature addition.

```
Acceptance Criteria: Player-Based Features
- Historical player averages
- Usage rate
- Minutes
"""

def test_adds_player_averages(self, empty_dataframe):
    """
    Test that player averages are added.
    
    Acceptance Criteria: Historical player averages
    """
    # Act: Add player stats
    result = add_player_stats_features(empty_dataframe)
    
    # Assert: Should have player averages
    assert 'player_points_avg' in result.columns
    assert 'player_rebounds_avg' in result.columns
    assert 'player_assists_avg' in result.columns

def test_adds_usage_rate(self, empty_dataframe):
    """
    Test that usage rate is added.
    
    Acceptance Criteria: Usage rate
    """
    # Act: Add player stats
    result = add_player_stats_features(empty_dataframe)
    
    # Assert: Should have usage rate
    assert 'player_usage_rate' in result.columns

def test_adds_minutes(self, empty_dataframe):
    """
    Test that minutes are added.
    
    Acceptance Criteria: Minutes
    """
    # Act: Add player stats
    result = add_player_stats_features(empty_dataframe)
    
    # Assert: Should have minutes
    assert 'player_minutes_avg' in result.columns

def test_usage_rate_in_valid_range(self, empty_dataframe):
    """Test that usage rate is between 0 and 1."""
    # Act: Add player stats
    result = add_player_stats_features(empty_dataframe)
    
    # Assert: Usage rate should be 0-1
    assert result['player_usage_rate'].min() >= 0
    assert result['player_usage_rate'].max() <= 1
```

# ====================

# Tests for add_betting_lines_features()

# ====================

class TestAddBettingLinesFeatures:
“””
Tests for betting lines feature addition.

```
Acceptance Criteria: Betting Market Features
- Spread line
- Total line
- Bookmaker odds
- Implied probabilities
"""

def test_adds_spread_line(self, empty_dataframe):
    """
    Test that spread line is added.
    
    Acceptance Criteria: Spread line
    """
    # Act: Add betting lines
    result = add_betting_lines_features(empty_dataframe)
    
    # Assert: Should have spread line
    assert 'spread_line' in result.columns

def test_adds_total_line(self, empty_dataframe):
    """
    Test that total line is added.
    
    Acceptance Criteria: Total line
    """
    # Act: Add betting lines
    result = add_betting_lines_features(empty_dataframe)
    
    # Assert: Should have total line
    assert 'total_line' in result.columns

def test_adds_bookmaker_odds(self, empty_dataframe):
    """
    Test that bookmaker odds are added.
    
    Acceptance Criteria: Bookmaker odds
    """
    # Act: Add betting lines
    result = add_betting_lines_features(empty_dataframe)
    
    # Assert: Should have odds
    assert 'home_moneyline_odds' in result.columns
    assert 'away_moneyline_odds' in result.columns

def test_adds_implied_probabilities(self, empty_dataframe):
    """
    Test that implied probabilities are calculated.
    
    Acceptance Criteria: Implied probabilities
    """
    # Act: Add betting lines
    result = add_betting_lines_features(empty_dataframe)
    
    # Assert: Should have implied probabilities
    assert 'home_implied_prob' in result.columns
    assert 'away_implied_prob' in result.columns

def test_implied_probabilities_between_0_and_1(self, empty_dataframe):
    """Test that implied probabilities are valid."""
    # Act: Add betting lines
    result = add_betting_lines_features(empty_dataframe)
    
    # Assert: Should be between 0 and 1
    assert result['home_implied_prob'].min() >= 0
    assert result['home_implied_prob'].max() <= 1
    assert result['away_implied_prob'].min() >= 0
    assert result['away_implied_prob'].max() <= 1
```

# ====================

# Tests for add_derived_features()

# ====================

class TestAddDerivedFeatures:
“””
Tests for derived features addition.

```
Acceptance Criteria: Derived Features
- Home vs away stat differentials
- Rolling averages
- Rest days
- Recent form
"""

def test_adds_stat_differentials(self, sample_dataframe):
    """
    Test that stat differentials are added.
    
    Acceptance Criteria: Home vs away stat differentials
    """
    # Act: Add derived features
    result = add_derived_features(sample_dataframe)
    
    # Assert: Should have differentials
    assert 'offensive_rating_diff' in result.columns
    assert 'defensive_rating_diff' in result.columns

def test_adds_rest_days(self, empty_dataframe):
    """
    Test that rest days are added.
    
    Acceptance Criteria: Rest days
    """
    # Act: Add derived features
    result = add_derived_features(empty_dataframe)
    
    # Assert: Should have rest days
    assert 'home_rest_days' in result.columns
    assert 'away_rest_days' in result.columns
    assert 'rest_days_diff' in result.columns

def test_adds_recent_form(self, empty_dataframe):
    """
    Test that recent form is added.
    
    Acceptance Criteria: Recent form
    """
    # Act: Add derived features
    result = add_derived_features(empty_dataframe)
    
    # Assert: Should have recent form
    assert 'home_win_streak' in result.columns
    assert 'away_win_streak' in result.columns
    assert 'home_last5_wins' in result.columns
    assert 'away_last5_wins' in result.columns
    assert 'recent_form_diff' in result.columns

def test_differential_calculation(self):
    """Test that differentials are calculated correctly."""
    # Arrange: DataFrame with known values
    df = pd.DataFrame({
        'home_offensive_rating': [115.0, 110.0],
        'away_offensive_rating': [110.0, 112.0],
        'home_defensive_rating': [105.0, 108.0],
        'away_defensive_rating': [110.0, 106.0]
    })
    
    # Act: Add derived features
    result = add_derived_features(df)
    
    # Assert: Check calculations
    assert result['offensive_rating_diff'].iloc[0] == 5.0  # 115 - 110
    assert result['offensive_rating_diff'].iloc[1] == -2.0  # 110 - 112
```

# ====================

# Tests for add_matchup_features()

# ====================

class TestAddMatchupFeatures:
“”“Tests for matchup features addition.”””

```
def test_adds_head_to_head(self, empty_dataframe):
    """Test that head-to-head record is added."""
    # Act: Add matchup features
    result = add_matchup_features(empty_dataframe)
    
    # Assert: Should have h2h
    assert 'home_h2h_win_pct' in result.columns

def test_adds_player_vs_team_stats(self, empty_dataframe):
    """Test that player vs team stats are added."""
    # Act: Add matchup features
    result = add_matchup_features(empty_dataframe)
    
    # Assert: Should have player vs team stats
    assert 'player_vs_team_ppg' in result.columns

def test_h2h_win_pct_in_valid_range(self, empty_dataframe):
    """Test that h2h win percentage is between 0 and 1."""
    # Act: Add matchup features
    result = add_matchup_features(empty_dataframe)
    
    # Assert: Should be 0-1
    assert result['home_h2h_win_pct'].min() >= 0
    assert result['home_h2h_win_pct'].max() <= 1
```

# ====================

# Tests for american_odds_to_probability()

# ====================

class TestAmericanOddsToProbability:
“”“Tests for odds conversion function.”””

```
def test_converts_positive_odds(self):
    """Test conversion of positive (underdog) odds."""
    # Arrange: Positive odds
    odds = pd.Series([+150, +200, +300])
    
    # Act: Convert to probability
    probs = american_odds_to_probability(odds)
    
    # Assert: Should be valid probabilities
    assert all(probs > 0)
    assert all(probs < 1)
    # +150 should be about 0.40
    assert 0.39 < probs.iloc[0] < 0.41

def test_converts_negative_odds(self):
    """Test conversion of negative (favorite) odds."""
    # Arrange: Negative odds
    odds = pd.Series([-110, -150, -200])
    
    # Act: Convert to probability
    probs = american_odds_to_probability(odds)
    
    # Assert: Should be valid probabilities
    assert all(probs > 0)
    assert all(probs < 1)
    # -150 should be about 0.60
    assert 0.59 < probs.iloc[1] < 0.61

def test_converts_mixed_odds(self):
    """Test conversion of mixed positive and negative odds."""
    # Arrange: Mixed odds
    odds = pd.Series([-110, +150, -200, +300])
    
    # Act: Convert
    probs = american_odds_to_probability(odds)
    
    # Assert: All should be valid
    assert all(probs > 0)
    assert all(probs < 1)

def test_returns_series(self):
    """Test that function returns a pandas Series."""
    # Arrange: Some odds
    odds = pd.Series([-110, +150])
    
    # Act: Convert
    probs = american_odds_to_probability(odds)
    
    # Assert: Should be Series
    assert isinstance(probs, pd.Series)

def test_preserves_index(self):
    """Test that index is preserved."""
    # Arrange: Series with custom index
    odds = pd.Series([-110, +150], index=['a', 'b'])
    
    # Act: Convert
    probs = american_odds_to_probability(odds)
    
    # Assert: Index should match
    assert list(probs.index) == ['a', 'b']
```

# ====================

# Tests for get_feature_columns()

# ====================

class TestGetFeatureColumns:
“”“Tests for feature column extraction.”””

```
def test_excludes_target_column(self):
    """Test that target column is excluded."""
    # Arrange: DataFrame with target
    df = pd.DataFrame({
        'feat1': [1, 2],
        'feat2': [3, 4],
        'target': [0, 1]
    })
    
    # Act: Get feature columns
    features = get_feature_columns(df, 'target')
    
    # Assert: Target should not be included
    assert 'target' not in features
    assert 'feat1' in features
    assert 'feat2' in features

def test_excludes_id_columns(self):
    """Test that ID columns are excluded."""
    # Arrange: DataFrame with IDs
    df = pd.DataFrame({
        'game_id': [1, 2],
        'team_id': [10, 20],
        'player_id': [100, 200],
        'feat1': [5, 6],
        'target': [0, 1]
    })
    
    # Act: Get feature columns
    features = get_feature_columns(df, 'target')
    
    # Assert: IDs should not be included
    assert 'game_id' not in features
    assert 'team_id' not in features
    assert 'player_id' not in features
    assert 'feat1' in features

def test_excludes_date_columns(self):
    """Test that date columns are excluded."""
    # Arrange: DataFrame with dates
    df = pd.DataFrame({
        'date': ['2024-01-01', '2024-01-02'],
        'game_date': ['2024-01-01', '2024-01-02'],
        'feat1': [1, 2],
        'target': [0, 1]
    })
    
    # Act: Get feature columns
    features = get_feature_columns(df, 'target')
    
    # Assert: Dates should not be included
    assert 'date' not in features
    assert 'game_date' not in features
    assert 'feat1' in features

def test_returns_list(self):
    """Test that function returns a list."""
    # Arrange: Simple DataFrame
    df = pd.DataFrame({
        'feat1': [1, 2],
        'target': [0, 1]
    })
    
    # Act: Get features
    features = get_feature_columns(df, 'target')
    
    # Assert: Should be list
    assert isinstance(features, list)
```

# ====================

# Integration Tests

# ====================

class TestFeatureEngineeringIntegration:
“”“Integration tests for complete workflows.”””

```
def test_complete_feature_preparation(self, empty_dataframe):
    """
    Test preparing features with all feature groups.
    
    This simulates actual training pipeline usage.
    """
    # Arrange: All feature groups
    all_groups = [
        FeatureGroup.TEAM_STATS,
        FeatureGroup.PLAYER_STATS,
        FeatureGroup.BETTING_LINES,
        FeatureGroup.DERIVED,
        FeatureGroup.MATCHUP
    ]
    
    # Act: Prepare all features
    result = prepare_features(empty_dataframe, all_groups)
    
    # Assert: Should have features from all groups
    assert 'home_offensive_rating' in result.columns  # Team stats
    assert 'player_points_avg' in result.columns      # Player stats
    assert 'spread_line' in result.columns            # Betting lines
    assert 'home_rest_days' in result.columns         # Derived
    assert 'home_h2h_win_pct' in result.columns      # Matchup

def test_feature_preparation_matches_training_usage(self):
    """Test that feature preparation works like in training."""
    # Arrange: Create training-like DataFrame
    df = pd.DataFrame({
        'game_id': range(100),
        'home_win': [0, 1] * 50  # Target
    })
    
    # Act: Prepare features for home_win model
    feature_groups = [
        FeatureGroup.TEAM_STATS,
        FeatureGroup.DERIVED,
        FeatureGroup.MATCHUP
    ]
    result = prepare_features(df, feature_groups)
    
    # Get feature columns
    features = get_feature_columns(result, 'home_win')
    
    # Assert: Should have features but not target
    assert len(features) > 0
    assert 'home_win' not in features
    assert 'game_id' not in features
```

if **name** == “**main**”:
pytest.main([**file**, “-v”])