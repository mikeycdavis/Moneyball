"""
Data loader for NBA training data.

Supports loading from:
- CSV files (for testing)
- Moneyball API (for production)

Acceptance Criteria:
- Pulls historical NBA data from Moneyball API
- Or loads CSV data locally for testing
- Data includes: Games, Team stats, Player stats, Betting lines and odds
"""

import logging
import requests
import pandas as pd
from pandas.errors import EmptyDataError
import numpy as np
from pathlib import Path
from typing import Optional, Dict, Any, List
from datetime import datetime, timedelta

logger = logging.getLogger(__name__)

# Moneyball API Configuration
MONEYBALL_API_BASE_URL = "https://localhost:7187"
MONEYBALL_API_TOKEN = "kFSApOPzV5PrhR8ToMa1fsXEblnqmLRgvHxJQ0gbPn1"


def load_training_data(
    source: str = "synthetic",
    filepath: Optional[str] = None,
    start_date: Optional[str] = None,
    end_date: Optional[str] = None
) -> pd.DataFrame:
    """
    Load training data from specified source.
    
    Acceptance Criteria:
    - Supports pulling from Moneyball API
    - Supports loading CSV locally
    - Returns data with all necessary columns
    
    Args:
        source: Data source ("synthetic", "csv", "moneyball")
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
    elif source == "moneyball":
        df = load_from_moneyball(start_date, end_date)
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
    
    try:
        df = pd.read_csv(filepath)
    except EmptyDataError:
        df = pd.DataFrame()  # fallback to empty dataframe
    
    logger.info(f"Loaded {len(df)} rows from CSV")
    
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


def load_from_moneyball(
    start_date: Optional[str] = None,
    end_date: Optional[str] = None
) -> pd.DataFrame:
    """
    Load training data from Moneyball API.
    
    Acceptance Criteria: Pulls historical NBA data from Moneyball API
    
    Args:
        start_date: Start date (YYYY-MM-DD). Defaults to 90 days ago.
        end_date: End date (YYYY-MM-DD). Defaults to today.
        
    Returns:
        DataFrame with training data including:
        - Game information
        - Team statistics
        - Player statistics
        - Betting odds
        
    Raises:
        requests.exceptions.RequestException: If API call fails
        ValueError: If response data is invalid
    """
    logger.info("Loading data from Moneyball API...")
    
    # Step 1: Set default date range if not provided
    # Default to last 90 days of data
    if not end_date:
        end_date = datetime.now().strftime('%Y-%m-%d')
    if not start_date:
        start_date = (datetime.now() - timedelta(days=90)).strftime('%Y-%m-%d')
    
    logger.info(f"Fetching data from {start_date} to {end_date}")
    
    # Step 2: Configure API request headers with authentication token
    headers = {
        'Authorization': f'Bearer {MONEYBALL_API_TOKEN}',
        'Content-Type': 'application/json',
        'Accept': 'application/json'
    }
    
    # Step 3: Fetch games data from Moneyball API
    logger.info("Fetching games data...")
    games_data = _fetch_games_by_date_range(start_date, end_date, headers)
    logger.info(f"Retrieved {len(games_data)} games")
    
    # Step 4: Fetch team statistics for the date range
    logger.info("Fetching team statistics...")
    team_stats = _fetch_team_stats(start_date, end_date, headers)
    logger.info(f"Retrieved team stats for {len(team_stats)} entries")
    
    # Step 5: Fetch player statistics for the date range
    logger.info("Fetching player statistics...")
    player_stats = _fetch_player_stats(start_date, end_date, headers)
    logger.info(f"Retrieved player stats for {len(player_stats)} entries")
    
    # Step 6: Fetch betting odds data
    logger.info("Fetching betting odds...")
    odds_data = _fetch_odds_data(start_date, end_date, headers)
    logger.info(f"Retrieved odds for {len(odds_data)} entries")
    
    # Step 7: Join all data sources into a single training DataFrame
    logger.info("Joining data from all sources...")
    df = join_data(games_data, team_stats, player_stats, odds_data)
    
    logger.info(f"Successfully loaded {len(df)} training samples from Moneyball API")
    logger.info(f"Columns: {list(df.columns)}")
    
    return df


def _fetch_games_by_date_range(
    start_date: str,
    end_date: str,
    headers: Dict[str, str]
) -> List[Dict[str, Any]]:
    """
    Fetch games data from Moneyball API GamesController.GetGamesByDateRange endpoint.
    
    Args:
        start_date: Start date (YYYY-MM-DD)
        end_date: End date (YYYY-MM-DD)
        headers: HTTP headers including authentication token
        
    Returns:
        List of game dictionaries
        
    Raises:
        requests.exceptions.RequestException: If API call fails
    """
    # Construct the API endpoint URL
    # Endpoint: GET /api/games/by-date-range
    url = f"{MONEYBALL_API_BASE_URL}/api/games/by-date-range"
    
    # Query parameters for date range
    params = {
        'startDate': start_date,
        'endDate': end_date
    }
    
    try:
        # Make GET request to Moneyball API
        # Note: verify=False is used for localhost SSL certificates
        # In production, use proper SSL certificate verification
        response = requests.get(
            url,
            params=params,
            headers=headers,
            timeout=30,
            verify=False  # For localhost development only
        )
        
        # Raise exception for HTTP errors (4xx, 5xx)
        response.raise_for_status()
        
        # Parse JSON response
        games_data = response.json()
        
        # Validate response structure
        if not isinstance(games_data, list):
            logger.warning("Expected list of games, got different structure")
            games_data = [games_data] if isinstance(games_data, dict) else []
        
        return games_data
        
    except requests.exceptions.SSLError as e: # pragma: no cover
        logger.error(f"SSL Error connecting to Moneyball API: {e}")
        logger.info("Tip: Ensure localhost SSL certificate is trusted or use verify=False for dev")
        raise
        
    except requests.exceptions.Timeout: # pragma: no cover
        logger.error(f"Request timed out connecting to {url}")
        raise
        
    except requests.exceptions.RequestException as e: # pragma: no cover
        logger.error(f"Error fetching games from Moneyball API: {e}")
        logger.error(f"URL: {url}")
        logger.error(f"Status Code: {getattr(e.response, 'status_code', 'N/A')}")
        raise


def _fetch_team_stats(
    start_date: str,
    end_date: str,
    headers: Dict[str, str]
) -> List[Dict[str, Any]]:
    """
    Fetch team statistics from Moneyball API.
    
    Args:
        start_date: Start date (YYYY-MM-DD)
        end_date: End date (YYYY-MM-DD)
        headers: HTTP headers including authentication token
        
    Returns:
        List of team statistics dictionaries
    """
    # Construct the API endpoint URL for team stats
    # Endpoint: GET /api/teams/stats/by-date-range
    url = f"{MONEYBALL_API_BASE_URL}/api/teams/stats/by-date-range"
    
    params = {
        'startDate': start_date,
        'endDate': end_date
    }
    
    try:
        response = requests.get(
            url,
            params=params,
            headers=headers,
            timeout=30,
            verify=False
        )
        
        response.raise_for_status()
        team_stats = response.json()
        
        return team_stats if isinstance(team_stats, list) else []
        
    except requests.exceptions.RequestException as e: # pragma: no cover
        logger.error(f"Error fetching team stats from Moneyball API: {e}")
        # Return empty list to allow training to continue with partial data
        return []


def _fetch_player_stats(
    start_date: str,
    end_date: str,
    headers: Dict[str, str]
) -> List[Dict[str, Any]]:
    """
    Fetch player statistics from Moneyball API.
    
    Args:
        start_date: Start date (YYYY-MM-DD)
        end_date: End date (YYYY-MM-DD)
        headers: HTTP headers including authentication token
        
    Returns:
        List of player statistics dictionaries
    """
    # Construct the API endpoint URL for player stats
    # Endpoint: GET /api/players/stats/by-date-range
    url = f"{MONEYBALL_API_BASE_URL}/api/players/stats/by-date-range"
    
    params = {
        'startDate': start_date,
        'endDate': end_date
    }
    
    try:
        response = requests.get(
            url,
            params=params,
            headers=headers,
            timeout=30,
            verify=False
        )
        
        response.raise_for_status()
        player_stats = response.json()
        
        return player_stats if isinstance(player_stats, list) else []
        
    except requests.exceptions.RequestException as e: # pragma: no cover
        logger.error(f"Error fetching player stats from Moneyball API: {e}")
        # Return empty list to allow training to continue with partial data
        return []


def _fetch_odds_data(
    start_date: str,
    end_date: str,
    headers: Dict[str, str]
) -> List[Dict[str, Any]]:
    """
    Fetch betting odds data from Moneyball API.
    
    Args:
        start_date: Start date (YYYY-MM-DD)
        end_date: End date (YYYY-MM-DD)
        headers: HTTP headers including authentication token
        
    Returns:
        List of betting odds dictionaries
    """
    # Construct the API endpoint URL for odds data
    # Endpoint: GET /api/odds/by-date-range
    url = f"{MONEYBALL_API_BASE_URL}/api/odds/by-date-range"
    
    params = {
        'startDate': start_date,
        'endDate': end_date
    }
    
    try:
        response = requests.get(
            url,
            params=params,
            headers=headers,
            timeout=30,
            verify=False
        )
        
        response.raise_for_status()
        odds_data = response.json()
        
        return odds_data if isinstance(odds_data, list) else []
        
    except requests.exceptions.RequestException as e: # pragma: no cover
        logger.error(f"Error fetching odds from Moneyball API: {e}")
        # Return empty list to allow training to continue with partial data
        return []


def join_data(
    games: List[Dict[str, Any]],
    team_stats: List[Dict[str, Any]],
    player_stats: List[Dict[str, Any]],
    odds: List[Dict[str, Any]]
) -> pd.DataFrame:
    """
    Join all data sources into a single training DataFrame.
    
    This function combines:
    - Game information (scores, dates, teams)
    - Team statistics (offensive/defensive ratings, pace, etc.)
    - Player statistics (points, rebounds, assists, etc.)
    - Betting odds (spreads, totals, moneylines)
    
    Args:
        games: List of game dictionaries
        team_stats: List of team statistics dictionaries
        player_stats: List of player statistics dictionaries
        odds: List of betting odds dictionaries
        
    Returns:
        DataFrame with all data joined on game_id
    """
    logger.info("Converting data to DataFrames...")
    
    # Step 1: Convert games list to DataFrame
    # Each game should have: game_id, date, home_team, away_team, scores, etc.
    if games:
        games_df = pd.DataFrame(games)
        logger.info(f"Games DataFrame: {len(games_df)} rows, columns: {list(games_df.columns)}")
    else:
        logger.warning("No games data to process")
        games_df = pd.DataFrame()
    
    # Step 2: Convert team stats to DataFrame
    # Each entry should have: game_id, team_id, offensive_rating, defensive_rating, etc.
    if team_stats:
        team_stats_df = pd.DataFrame(team_stats)
        logger.info(f"Team stats DataFrame: {len(team_stats_df)} rows")
        
        # Pivot team stats to have home and away columns
        # This creates columns like: home_offensive_rating, away_offensive_rating
        team_stats_df = _pivot_team_stats(team_stats_df)
    else:
        logger.warning("No team stats data to process")
        team_stats_df = pd.DataFrame()
    
    # Step 3: Convert player stats to DataFrame
    # Each entry should have: game_id, player_id, points, rebounds, assists, etc.
    if player_stats:
        player_stats_df = pd.DataFrame(player_stats)
        logger.info(f"Player stats DataFrame: {len(player_stats_df)} rows")
        
        # Aggregate player stats per game (e.g., star player stats)
        player_stats_df = _aggregate_player_stats(player_stats_df)
    else:
        logger.warning("No player stats data to process")
        player_stats_df = pd.DataFrame()
    
    # Step 4: Convert odds to DataFrame
    # Each entry should have: game_id, spread_line, total_line, moneyline_odds, etc.
    if odds:
        odds_df = pd.DataFrame(odds)
        logger.info(f"Odds DataFrame: {len(odds_df)} rows")
    else:
        logger.warning("No odds data to process")
        odds_df = pd.DataFrame()
    
    # Step 5: Join all DataFrames on game_id
    logger.info("Joining all data sources...")
    
    # Start with games as the base
    if games_df.empty or 'game_id' not in games_df.columns:
        logger.error("Cannot create training data without games")
        return pd.DataFrame()
    
    df = games_df
    
    # Left join team stats
    if not team_stats_df.empty and 'game_id' in team_stats_df.columns:
        df = df.merge(team_stats_df, on='game_id', how='left')
        logger.info(f"After team stats join: {len(df)} rows, {len(df.columns)} columns")
    
    # Left join player stats
    if not player_stats_df.empty and 'game_id' in player_stats_df.columns:
        df = df.merge(player_stats_df, on='game_id', how='left')
        logger.info(f"After player stats join: {len(df)} rows, {len(df.columns)} columns")
    
    # Left join odds
    if not odds_df.empty and 'game_id' in odds_df.columns:
        df = df.merge(odds_df, on='game_id', how='left')
        logger.info(f"After odds join: {len(df)} rows, {len(df.columns)} columns")
    
    # Step 6: Data quality checks
    logger.info(f"Final DataFrame: {len(df)} rows, {len(df.columns)} columns")
    logger.info(f"Missing values per column:\n{df.isnull().sum()}")
    
    return df


def _pivot_team_stats(team_stats_df: pd.DataFrame) -> pd.DataFrame:
    """
    Pivot team statistics to have separate columns for home and away teams.
    
    Converts:
        game_id, team_id, is_home, offensive_rating, defensive_rating
    To:
        game_id, home_offensive_rating, away_offensive_rating, home_defensive_rating, away_defensive_rating
    
    Args:
        team_stats_df: DataFrame with team statistics
        
    Returns:
        Pivoted DataFrame with home/away prefixed columns
    """
    # Assuming the API returns an 'is_home' or 'home_away' indicator
    # Adjust column names based on actual API response structure
    
    if 'is_home' not in team_stats_df.columns:
        logger.warning("is_home column not found in team stats, skipping pivot")
        return team_stats_df
    
    # Separate home and away team stats
    home_stats = team_stats_df[team_stats_df['is_home'] == True].copy()
    away_stats = team_stats_df[team_stats_df['is_home'] == False].copy()
    
    # Rename columns to add home/away prefix
    stat_columns = [col for col in home_stats.columns if col not in ['game_id', 'team_id', 'is_home']]
    
    home_stats = home_stats.rename(columns={col: f'home_{col}' for col in stat_columns})
    away_stats = away_stats.rename(columns={col: f'away_{col}' for col in stat_columns})
    
    # Merge home and away stats
    team_stats_pivoted = home_stats[['game_id'] + [f'home_{col}' for col in stat_columns]].merge(
        away_stats[['game_id'] + [f'away_{col}' for col in stat_columns]],
        on='game_id',
        how='outer'
    )
    
    return team_stats_pivoted


def _aggregate_player_stats(player_stats_df: pd.DataFrame) -> pd.DataFrame:
    """
    Aggregate player statistics per game.
    
    For training, we typically want stats for key players (e.g., top scorer).
    This function aggregates player stats to game level.
    
    Args:
        player_stats_df: DataFrame with player statistics
        
    Returns:
        Aggregated DataFrame with game-level player stats
    """
    # For each game, select the player with most points (star player)
    # Or aggregate all players' stats
    
    if 'game_id' not in player_stats_df.columns:
        logger.warning("game_id column not found in player stats")
        return pd.DataFrame()
    
    # Group by game and take the player with highest points
    # This gives us the "star player" stats for each game
    player_stats_df = player_stats_df.sort_values('points', ascending=False)
    player_stats_agg = player_stats_df.groupby('game_id').first().reset_index()
    
    # Rename columns to be clear these are player stats
    stat_columns = [col for col in player_stats_agg.columns if col not in ['game_id', 'player_id', 'team_id']]
    player_stats_agg = player_stats_agg.rename(columns={col: f'player_{col}' for col in stat_columns})
    
    return player_stats_agg


# Note: Suppress SSL warnings for localhost development
# Remove this in production with proper SSL certificates
import urllib3
urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning) # pragma: no cover