"""
Data loader for NBA training data.

Supports loading from:
- CSV files (for testing)
- SportsRadar API (for production)

Acceptance Criteria:
- Pulls historical NBA data from SportsRadar API
- Or loads CSV data locally for testing
- Data includes: Games, Team stats, Player stats, Betting lines and odds
"""

import logging
import pandas as pd
import numpy as np
from pathlib import Path
from typing import Optional, Dict, Any
from datetime import datetime, timedelta

logger = logging.getLogger(__name__)


def load_training_data(
    source: str = "synthetic",
    filepath: Optional[str] = None,
    start_date: Optional[str] = None,
    end_date: Optional[str] = None
) -> pd.DataFrame:
    """
    Load training data from specified source.
    
    Acceptance Criteria:
    - Supports pulling from SportsRadar API
    - Supports loading CSV locally
    - Returns data with all necessary columns
    
    Args:
        source: Data source ("synthetic", "csv", "sportsradar")
        filepath: Path to CSV file (if source="csv")
        start_date: Start date for data (YYYY-MM-DD)
        end_date: End date for data (YYYY-MM-DD)
        
    Returns:
        DataFrame with training data including all targets
    """
    logger.info(f"Loading training data from source: {source}")
    
    if source == "synthetic":
        df = generate_synthetic_training_data()
    elif source == "csv":
        if not filepath:
            raise ValueError("filepath required when source='csv'")
        df = load_from_csv(filepath)
    elif source == "sportsradar":
        df = load_from_sportsradar(start_date, end_date)
    else:
        raise ValueError(f"Unsupported data source: {source}")
    
    # Add all target columns
    df = add_target_columns(df)
    
    logger.info(f"Loaded {len(df)} training samples")
    logger.info(f"Columns: {list(df.columns)}")
    
    return df


def generate_synthetic_training_data(n_samples: int = 10000) -> pd.DataFrame:
    """
    Generate synthetic NBA game data for training/testing.
    
    Creates realistic data with proper correlations between features.
    
    Args:
        n_samples: Number of games to generate
        
    Returns:
        DataFrame with synthetic game data
    """
    logger.info(f"Generating {n_samples} synthetic training samples...")
    
    np.random.seed(42)
    
    # Base team statistics
    data = {
        # Game identifiers
        'game_id': range(n_samples),
        'game_date': [
            datetime(2023, 10, 1) + timedelta(days=i % 180) 
            for i in range(n_samples)
        ],
        
        # Team offensive statistics
        'home_offensive_rating': np.random.uniform(105, 118, n_samples),
        'away_offensive_rating': np.random.uniform(105, 118, n_samples),
        'home_points_avg': np.random.uniform(105, 118, n_samples),
        'away_points_avg': np.random.uniform(105, 118, n_samples),
        
        # Team defensive statistics  
        'home_defensive_rating': np.random.uniform(105, 118, n_samples),
        'away_defensive_rating': np.random.uniform(105, 118, n_samples),
        'home_points_allowed_avg': np.random.uniform(105, 118, n_samples),
        'away_points_allowed_avg': np.random.uniform(105, 118, n_samples),
        
        # Pace and tempo
        'home_pace': np.random.uniform(96, 104, n_samples),
        'away_pace': np.random.uniform(96, 104, n_samples),
        
        # Rebounds and turnovers
        'home_rebounds_avg': np.random.uniform(42, 48, n_samples),
        'away_rebounds_avg': np.random.uniform(42, 48, n_samples),
        'home_turnovers_avg': np.random.uniform(12, 16, n_samples),
        'away_turnovers_avg': np.random.uniform(12, 16, n_samples),
        
        # Rest and scheduling
        'home_rest_days': np.random.randint(0, 5, n_samples),
        'away_rest_days': np.random.randint(0, 5, n_samples),
        
        # Recent form
        'home_win_streak': np.random.randint(-5, 6, n_samples),
        'away_win_streak': np.random.randint(-5, 6, n_samples),
        'home_last5_wins': np.random.randint(0, 6, n_samples),
        'away_last5_wins': np.random.randint(0, 6, n_samples),
        
        # Head-to-head
        'home_h2h_win_pct': np.random.uniform(0.3, 0.7, n_samples),
        
        # Betting lines
        'spread_line': np.random.uniform(-12, 12, n_samples),
        'total_line': np.random.uniform(210, 235, n_samples),
        'home_moneyline_odds': np.random.uniform(-250, 250, n_samples),
        'away_moneyline_odds': np.random.uniform(-250, 250, n_samples),
        
        # Player statistics (for player prop models)
        'player_points_avg': np.random.uniform(18, 32, n_samples),
        'player_rebounds_avg': np.random.uniform(6, 12, n_samples),
        'player_assists_avg': np.random.uniform(4, 10, n_samples),
        'player_usage_rate': np.random.uniform(0.20, 0.35, n_samples),
        'player_minutes_avg': np.random.uniform(28, 38, n_samples),
        'player_vs_team_ppg': np.random.uniform(18, 35, n_samples),
        
        # Actual game outcomes (will be used to create targets)
        'home_final_score': np.random.uniform(100, 125, n_samples),
        'away_final_score': np.random.uniform(100, 125, n_samples),
        'overtime_occurred': np.random.choice([0, 1], n_samples, p=[0.93, 0.07]),
        'first_to_20_was_home': np.random.choice([0, 1], n_samples, p=[0.55, 0.45]),
        'player_actual_points': np.random.uniform(15, 35, n_samples),
        'player_actual_rebounds': np.random.uniform(4, 14, n_samples),
    }
    
    df = pd.DataFrame(data)
    
    # Add realistic correlations
    # Better offensive rating -> more points
    df['home_final_score'] = (
        df['home_final_score'] * 0.3 + 
        df['home_offensive_rating'] * 0.7 +
        np.random.normal(0, 5, n_samples)
    )
    
    df['away_final_score'] = (
        df['away_final_score'] * 0.3 + 
        df['away_offensive_rating'] * 0.7 +
        np.random.normal(0, 5, n_samples)
    )
    
    logger.info("Synthetic data generation complete")
    
    return df


def load_from_csv(filepath: str) -> pd.DataFrame:
    """
    Load training data from CSV file.
    
    Acceptance Criteria: Load CSV data locally for testing
    
    Args:
        filepath: Path to CSV file
        
    Returns:
        DataFrame with training data
    """
    logger.info(f"Loading data from CSV: {filepath}")
    
    if not Path(filepath).exists():
        raise FileNotFoundError(f"CSV file not found: {filepath}")
    
    df = pd.read_csv(filepath)
    
    logger.info(f"Loaded {len(df)} rows from CSV")
    
    return df


def load_from_sportsradar(
    start_date: Optional[str] = None,
    end_date: Optional[str] = None
) -> pd.DataFrame:
    """
    Load training data from SportsRadar API.
    
    Acceptance Criteria: Pulls historical NBA data from SportsRadar API
    
    Args:
        start_date: Start date (YYYY-MM-DD)
        end_date: End date (YYYY-MM-DD)
        
    Returns:
        DataFrame with training data
        
    Note: This is a placeholder. Full implementation would:
    1. Import SportsRadar client
    2. Fetch games, team stats, player stats, odds
    3. Join data into training format
    4. Handle rate limiting and pagination
    """
    logger.info("Loading data from SportsRadar API...")
    logger.warning(
        "SportsRadar integration not yet implemented. "
        "Using synthetic data instead."
    )
    
    # TODO: Implement SportsRadar API integration
    # from moneyball_ml_python.data.sportsradar_client import SportsRadarClient
    # client = SportsRadarClient()
    # games = client.get_games(start_date, end_date)
    # team_stats = client.get_team_stats(start_date, end_date)
    # player_stats = client.get_player_stats(start_date, end_date)
    # odds = client.get_odds(start_date, end_date)
    # df = join_data(games, team_stats, player_stats, odds)
    
    # For now, return synthetic data
    df = generate_synthetic_training_data()
    
    return df


def add_target_columns(df: pd.DataFrame) -> pd.DataFrame:
    """
    Add target columns for all models.
    
    Creates binary target variables from actual game outcomes.
    
    Acceptance Criteria: Creates targets for all model types:
    - home_win
    - home_cover
    - total_over
    - player_points_over_X
    - player_rebounds_over_X
    - overtime_yes
    - first_team_to_20_home
    
    Args:
        df: DataFrame with game data
        
    Returns:
        DataFrame with added target columns
    """
    logger.debug("Adding target columns...")
    
    # Win/Loss target
    df['home_win'] = (df['home_final_score'] > df['away_final_score']).astype(int)
    
    # Point spread target
    home_margin = df['home_final_score'] - df['away_final_score']
    df['home_cover'] = (home_margin > df['spread_line']).astype(int)
    
    # Over/Under target
    total_points = df['home_final_score'] + df['away_final_score']
    df['total_over'] = (total_points > df['total_line']).astype(int)
    
    # Player prop targets
    df['player_points_over_25'] = (df['player_actual_points'] > 25).astype(int)
    df['player_rebounds_over_10'] = (df['player_actual_rebounds'] > 10).astype(int)
    
    # Event outcome targets
    df['overtime_yes'] = df['overtime_occurred'].astype(int)
    df['first_team_to_20_home'] = df['first_to_20_was_home'].astype(int)
    
    logger.debug(f"Added {7} target columns")
    
    return df


def validate_training_data(df: pd.DataFrame) -> None:
    """
    Validate that training data has required columns and quality.
    
    Args:
        df: DataFrame to validate
        
    Raises:
        ValueError: If data is invalid
    """
    required_columns = [
        'home_offensive_rating',
        'away_offensive_rating',
        'home_defensive_rating',
        'away_defensive_rating'
    ]
    
    missing_columns = [col for col in required_columns if col not in df.columns]
    
    if missing_columns:
        raise ValueError(f"Missing required columns: {missing_columns}")
    
    if len(df) < 100:
        raise ValueError(f"Insufficient training data: {len(df)} rows (need >= 100)")
    
    # Check for excessive missing values
    missing_pct = df.isnull().mean()
    high_missing = missing_pct[missing_pct > 0.5]
    
    if len(high_missing) > 0:
        logger.warning(
            f"Columns with >50% missing values: {list(high_missing.index)}"
        )
    
    logger.info("Data validation passed")