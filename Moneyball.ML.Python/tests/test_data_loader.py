"""
Unit tests for data_loader.py

Tests all functions in the data loader module:
- load_training_data() - Main entry point
- generate_synthetic_training_data() - Synthetic data generation
- load_from_csv() - CSV loading
- load_from_moneyball() - Moneyball API integration
- _fetch_games_by_date_range() - Games API call
- _fetch_team_stats() - Team stats API call
- _fetch_player_stats() - Player stats API call
- _fetch_odds_data() - Odds API call
- join_data() - Data joining logic
- _pivot_team_stats() - Team stats pivoting
- _aggregate_player_stats() - Player stats aggregation
- add_target_columns() - Target column creation
- validate_training_data() - Data validation

Test Structure:
- Test basic functionality (happy path)
- Test edge cases
- Test error conditions
- Test data quality and structure
- Test API integration
"""

import pytest
import pandas as pd
import numpy as np
from pathlib import Path
from datetime import datetime
import tempfile
import os
from unittest.mock import Mock, patch
import requests

# Import the module to test
from moneyball_ml_python.data.data_loader import (
    load_training_data,
    generate_synthetic_training_data,
    load_from_csv,
    load_from_moneyball,
    _fetch_games_by_date_range,
    _fetch_team_stats,
    _fetch_player_stats,
    _fetch_odds_data,
    join_data,
    _pivot_team_stats,
    _aggregate_player_stats,
    add_target_columns,
    validate_training_data,
    MONEYBALL_API_BASE_URL,
    MONEYBALL_API_TOKEN
)


# ====================
# Fixtures
# ====================

@pytest.fixture
def sample_dataframe():
    """
    Create a sample DataFrame for testing.
    
    This fixture provides a minimal valid DataFrame that can be used
    to test functions that expect game data.
    
    Returns:
        pd.DataFrame with minimal game data
    """
    return pd.DataFrame({
        'game_id': [1, 2, 3],
        'game_date': [datetime(2024, 1, 1), datetime(2024, 1, 2), datetime(2024, 1, 3)],
        'home_offensive_rating': [115.0, 110.0, 112.0],
        'away_offensive_rating': [108.0, 112.0, 110.0],
        'home_defensive_rating': [105.0, 108.0, 107.0],
        'away_defensive_rating': [110.0, 106.0, 108.0],
        'home_final_score': [120.0, 105.0, 115.0],
        'away_final_score': [110.0, 108.0, 112.0],
        'spread_line': [-5.0, 2.0, -3.0],
        'total_line': [225.0, 210.0, 220.0],
        'overtime_occurred': [0, 0, 1],
        'first_to_20_was_home': [1, 0, 1],
        'player_actual_points': [28.0, 22.0, 30.0],
        'player_actual_rebounds': [12.0, 8.0, 11.0]
    })


@pytest.fixture
def temp_csv_file(sample_dataframe):
    """
    Create a temporary CSV file for testing.
    
    This fixture creates a CSV file in a temporary directory,
    writes sample data to it, and cleans it up after the test.
    
    Args:
        sample_dataframe: Sample data to write to CSV
        
    Yields:
        str: Path to temporary CSV file
    """
    # Create temporary file
    temp_file = tempfile.NamedTemporaryFile(mode='w', delete=False, suffix='.csv')
    temp_path = temp_file.name
    
    # Write sample data to CSV
    sample_dataframe.to_csv(temp_path, index=False)
    temp_file.close()
    
    # Provide path to test
    yield temp_path
    
    # Cleanup after test
    os.unlink(temp_path)


@pytest.fixture
def mock_games_response():
    """
    Create mock games API response.
    
    Returns:
        list: Mock games data from Moneyball API
    """
    return [
        {
            'game_id': 'game_1',
            'game_date': '2024-01-15',
            'home_team': 'Lakers',
            'away_team': 'Celtics',
            'home_final_score': 110,
            'away_final_score': 105
        },
        {
            'game_id': 'game_2',
            'game_date': '2024-01-16',
            'home_team': 'Warriors',
            'away_team': 'Heat',
            'home_final_score': 115,
            'away_final_score': 108
        }
    ]


@pytest.fixture
def mock_team_stats_response():
    """
    Create mock team stats API response.
    
    Returns:
        list: Mock team stats data
    """
    return [
        {
            'game_id': 'game_1',
            'team_id': 'lakers',
            'is_home': True,
            'offensive_rating': 115.2,
            'defensive_rating': 110.5,
            'pace': 100.3
        },
        {
            'game_id': 'game_1',
            'team_id': 'celtics',
            'is_home': False,
            'offensive_rating': 112.1,
            'defensive_rating': 113.2,
            'pace': 98.7
        }
    ]


# ====================
# Tests for load_training_data()
# ====================

class TestLoadTrainingData:
    """Tests for the main load_training_data() function."""
    
    def test_load_synthetic_data(self):
        """
        Test loading synthetic training data.
        
        Acceptance Criteria:
        - Returns DataFrame with data
        - Includes all target columns
        - Has expected structure
        """
        # Act: Load synthetic data
        df = load_training_data(source="synthetic")
        
        # Assert: Check basic structure
        assert isinstance(df, pd.DataFrame), "Should return DataFrame"
        assert len(df) > 0, "Should have rows"
        assert len(df.columns) > 0, "Should have columns"
        
        # Assert: Check target columns exist
        expected_targets = [
            'home_win', 'home_cover', 'total_over',
            'player_points_over_25', 'player_rebounds_over_10',
            'overtime_yes', 'first_team_to_20_home'
        ]
        for target in expected_targets:
            assert target in df.columns, f"Should have {target} target column"
    
    def test_load_from_csv_source(self, temp_csv_file):
        """
        Test loading data from CSV file.
        
        Acceptance Criteria:
        - Loads data from CSV file
        - filepath parameter is required
        - Returns DataFrame with correct data
        """
        # Act: Load from CSV
        df = load_training_data(source="csv", filepath=temp_csv_file)
        
        # Assert: Data loaded correctly
        assert isinstance(df, pd.DataFrame)
        assert len(df) == 3, "Should load all 3 rows from CSV"
        assert 'home_offensive_rating' in df.columns
    
    def test_load_from_csv_requires_filepath(self):
        """
        Test that CSV source requires filepath parameter.
        
        Should raise ValueError if filepath not provided.
        """
        # Act & Assert: Should raise error
        with pytest.raises(ValueError, match="filepath required"):
            load_training_data(source="csv", filepath=None)
    
    @patch('moneyball_ml_python.data.data_loader.load_from_moneyball')
    def test_load_from_moneyball_source(self, mock_load_moneyball):
        """
        Test loading from Moneyball API.
        
        Acceptance Criteria: Supports pulling from Moneyball API
        """
        # Arrange: Mock Moneyball API response
        mock_df = pd.DataFrame({
            'game_id': [1],
            'home_offensive_rating': [115.0],
            'away_offensive_rating': [110.0],
            'home_defensive_rating': [105.0],
            'away_defensive_rating': [108.0],
            'home_final_score': [120.0],
            'away_final_score': [110.0],
            'spread_line': [-5.0],
            'total_line': [225.0],
            'overtime_occurred': [0],
            'first_to_20_was_home': [1],
            'player_actual_points': [28.0],
            'player_actual_rebounds': [12.0]
        })
        mock_load_moneyball.return_value = mock_df
        
        # Act: Load from Moneyball
        df = load_training_data(source="moneyball")
        
        # Assert: Should call Moneyball function
        mock_load_moneyball.assert_called_once()
        assert isinstance(df, pd.DataFrame)
    
    def test_invalid_source_raises_error(self):
        """
        Test that invalid source parameter raises ValueError.
        """
        # Act & Assert: Invalid source should raise error
        with pytest.raises(ValueError, match="Unsupported data source"):
            load_training_data(source="invalid_source")
    
    def test_date_parameters_passed_to_moneyball(self):
        """
        Test that start_date and end_date are passed to Moneyball API.
        """
        # Arrange: Mock Moneyball API
        with patch('moneyball_ml_python.data.data_loader.load_from_moneyball') as mock_load:
            mock_load.return_value = pd.DataFrame({
                'game_id': [1],
                'home_offensive_rating': [115.0],
                'away_offensive_rating': [110.0],
                'home_defensive_rating': [105.0],
                'away_defensive_rating': [108.0],
                'home_final_score': [120.0],
                'away_final_score': [110.0],
                'spread_line': [-5.0],
                'total_line': [225.0],
                'overtime_occurred': [0],
                'first_to_20_was_home': [1],
                'player_actual_points': [28.0],
                'player_actual_rebounds': [12.0]
            })
            
            # Act: Load with date parameters
            load_training_data(
                source="moneyball",
                start_date="2024-01-01",
                end_date="2024-01-31"
            )
            
            # Assert: Should pass dates to Moneyball function
            mock_load.assert_called_once_with("2024-01-01", "2024-01-31")


# ====================
# Tests for load_from_csv()
# ====================

class TestLoadFromCSV:
    """Tests for CSV file loading."""
    
    def test_loads_csv_successfully(self, temp_csv_file):
        """Test basic CSV loading functionality."""
        # Act: Load CSV
        df = load_from_csv(temp_csv_file)
        
        # Assert: Data loaded
        assert isinstance(df, pd.DataFrame)
        assert len(df) == 3, "Should load all rows"
    
    def test_raises_error_if_file_not_found(self):
        """Test that FileNotFoundError is raised for missing file."""
        # Act & Assert: Should raise FileNotFoundError
        with pytest.raises(FileNotFoundError, match="CSV file not found"):
            load_from_csv("nonexistent_file.csv")
    
    def test_handles_empty_csv(self):
        """
        Test loading an empty CSV file.
        
        Should handle EmptyDataError gracefully.
        """
        # Arrange: Create empty CSV
        temp_file = tempfile.NamedTemporaryFile(mode='w', delete=False, suffix='.csv')
        temp_path = temp_file.name
        temp_file.write("")  # Empty file
        temp_file.close()
        
        try:
            # Act: Load empty CSV
            df = load_from_csv(temp_path)
            
            # Assert: Should return empty DataFrame
            assert isinstance(df, pd.DataFrame)
            assert len(df) == 0
        finally:
            # Cleanup
            os.unlink(temp_path)


# ====================
# Tests for load_from_moneyball()
# ====================

class TestLoadFromMoneyball:
    """Tests for Moneyball API integration."""
    
    @patch('moneyball_ml_python.data.data_loader._fetch_games_by_date_range')
    @patch('moneyball_ml_python.data.data_loader._fetch_team_stats')
    @patch('moneyball_ml_python.data.data_loader._fetch_player_stats')
    @patch('moneyball_ml_python.data.data_loader._fetch_odds_data')
    @patch('moneyball_ml_python.data.data_loader.join_data')
    def test_loads_data_from_moneyball_api(
        self,
        mock_join,
        mock_odds,
        mock_player,
        mock_team,
        mock_games,
        mock_games_response
    ):
        """
        Test successful data loading from Moneyball API.
        
        Acceptance Criteria: Pulls historical NBA data from Moneyball API
        """
        # Arrange: Mock all API responses
        mock_games.return_value = mock_games_response
        mock_team.return_value = []
        mock_player.return_value = []
        mock_odds.return_value = []
        mock_join.return_value = pd.DataFrame(mock_games_response)
        
        # Act: Load data
        df = load_from_moneyball(start_date='2024-01-01', end_date='2024-01-31')
        
        # Assert: Should call all fetch functions
        mock_games.assert_called_once()
        mock_team.assert_called_once()
        mock_player.assert_called_once()
        mock_odds.assert_called_once()
        mock_join.assert_called_once()
        
        # Should return DataFrame
        assert isinstance(df, pd.DataFrame)
        assert len(df) > 0
    
    @patch('moneyball_ml_python.data.data_loader._fetch_games_by_date_range')
    @patch('moneyball_ml_python.data.data_loader._fetch_team_stats')
    @patch('moneyball_ml_python.data.data_loader._fetch_player_stats')
    @patch('moneyball_ml_python.data.data_loader._fetch_odds_data')
    @patch('moneyball_ml_python.data.data_loader.join_data')
    def test_uses_default_date_range(
        self,
        mock_join,
        mock_odds,
        mock_player,
        mock_team,
        mock_games
    ):
        """
        Test that default date range is used when not provided.
        
        Default should be last 90 days.
        """
        # Arrange: Mock responses
        mock_games.return_value = []
        mock_team.return_value = []
        mock_player.return_value = []
        mock_odds.return_value = []
        mock_join.return_value = pd.DataFrame()
        
        # Act: Load without dates
        load_from_moneyball()
        
        # Assert: Should call with default dates
        call_args = mock_games.call_args[0]
        start_date = call_args[0]
        end_date = call_args[1]
        
        # Dates should be strings in YYYY-MM-DD format
        assert isinstance(start_date, str)
        assert isinstance(end_date, str)
        assert len(start_date) == 10
        assert len(end_date) == 10


# ====================
# Tests for _fetch_games_by_date_range()
# ====================

class TestFetchGamesByDateRange:
    """Tests for games API fetch function."""
    
    @patch('requests.get')
    def test_fetches_games_successfully(self, mock_get, mock_games_response):
        """
        Test successful games API call.
        
        Verifies that games are fetched from Moneyball API.
        """
        # Arrange: Mock successful response
        mock_response = Mock()
        mock_response.status_code = 200
        mock_response.json.return_value = mock_games_response
        mock_get.return_value = mock_response
        
        headers = {'Authorization': f'Bearer {MONEYBALL_API_TOKEN}'}
        
        # Act: Fetch games
        result = _fetch_games_by_date_range('2024-01-01', '2024-01-31', headers)
        
        # Assert: Should return games list
        assert isinstance(result, list)
        assert len(result) == 2
        assert result[0]['game_id'] == 'game_1'
    
    @patch('requests.get')
    def test_uses_correct_api_endpoint(self, mock_get):
        """
        Test that correct Moneyball API endpoint is called.
        
        Endpoint should be: /api/games/by-date-range
        """
        # Arrange
        mock_response = Mock()
        mock_response.status_code = 200
        mock_response.json.return_value = []
        mock_get.return_value = mock_response
        
        headers = {'Authorization': f'Bearer {MONEYBALL_API_TOKEN}'}
        
        # Act: Fetch games
        _fetch_games_by_date_range('2024-01-01', '2024-01-31', headers)
        
        # Assert: Check URL
        call_args = mock_get.call_args
        url = call_args[0][0]
        
        assert MONEYBALL_API_BASE_URL in url
        assert 'by-date-range' in url
    
    @patch('requests.get')
    def test_includes_authentication_header(self, mock_get):
        """
        Test that authentication token is included in request.
        
        Header should be: Authorization: Bearer {token}
        """
        # Arrange
        mock_response = Mock()
        mock_response.status_code = 200
        mock_response.json.return_value = []
        mock_get.return_value = mock_response
        
        headers = {'Authorization': f'Bearer {MONEYBALL_API_TOKEN}'}
        
        # Act: Fetch games
        _fetch_games_by_date_range('2024-01-01', '2024-01-31', headers)
        
        # Assert: Check headers in request
        call_kwargs = mock_get.call_args[1]
        request_headers = call_kwargs.get('headers', {})
        
        assert 'Authorization' in request_headers
        assert MONEYBALL_API_TOKEN in request_headers['Authorization']
    
    @patch('requests.get')
    def test_handles_ssl_error(self, mock_get):
        """
        Test SSL error handling for localhost development.
        
        Should raise SSLError for SSL certificate issues.
        """
        # Arrange: Mock SSL error
        mock_get.side_effect = requests.exceptions.SSLError("SSL verification failed")
        
        headers = {'Authorization': f'Bearer {MONEYBALL_API_TOKEN}'}
        
        # Act & Assert: Should raise SSL error
        with pytest.raises(requests.exceptions.SSLError):
            _fetch_games_by_date_range('2024-01-01', '2024-01-31', headers)
    
    @patch('requests.get')
    def test_handles_timeout_error(self, mock_get):
        """
        Test timeout error handling.
        
        Timeout is set to 30 seconds.
        """
        # Arrange: Mock timeout
        mock_get.side_effect = requests.exceptions.Timeout("Request timed out")
        
        headers = {'Authorization': f'Bearer {MONEYBALL_API_TOKEN}'}
        
        # Act & Assert: Should raise timeout error
        with pytest.raises(requests.exceptions.Timeout):
            _fetch_games_by_date_range('2024-01-01', '2024-01-31', headers)


# ====================
# Tests for _fetch_team_stats()
# ====================

class TestFetchTeamStats:
    """Tests for team stats API fetch function."""
    
    @patch('requests.get')
    def test_returns_empty_list_on_error(self, mock_get):
        """
        Test graceful error handling.
        
        Should return empty list instead of crashing (allows training to continue).
        """
        # Arrange: Mock error
        mock_get.side_effect = requests.exceptions.RequestException("API error")
        
        headers = {'Authorization': f'Bearer {MONEYBALL_API_TOKEN}'}
        
        # Act: Fetch team stats
        result = _fetch_team_stats('2024-01-01', '2024-01-31', headers)
        
        # Assert: Should return empty list (not raise error)
        assert result == []


# ====================
# Tests for join_data()
# ====================

class TestJoinData:
    """Tests for data joining function."""
    
    def test_returns_empty_df_without_games(self):
        """
        Test that empty DataFrame is returned when no games.
        
        Games are required as the base for joining.
        """
        # Act: Join with no games
        df = join_data([], [], [], [])
        
        # Assert: Should return empty DataFrame
        assert isinstance(df, pd.DataFrame)
        assert len(df) == 0
    
    def test_handles_partial_data(self, mock_games_response):
        """
        Test joining with partial data (some sources empty).
        
        Should still work with only games data.
        """
        # Act: Join with only games data
        df = join_data(mock_games_response, [], [], [])
        
        # Assert: Should still work
        assert isinstance(df, pd.DataFrame)
        assert len(df) > 0


# ====================
# Tests for _pivot_team_stats()
# ====================

class TestPivotTeamStats:
    """Tests for team stats pivoting function."""
    
    def test_pivots_team_stats_to_home_away(self):
        """
        Test pivoting team stats into home/away columns.
        
        Converts: offensive_rating, is_home
        To: home_offensive_rating, away_offensive_rating
        """
        # Arrange: Team stats DataFrame
        team_stats_df = pd.DataFrame([
            {'game_id': 'game_1', 'team_id': 'lakers', 'is_home': True, 'offensive_rating': 115.2},
            {'game_id': 'game_1', 'team_id': 'celtics', 'is_home': False, 'offensive_rating': 112.1}
        ])
        
        # Act: Pivot
        result = _pivot_team_stats(team_stats_df)
        
        # Assert: Should have home/away columns
        assert 'game_id' in result.columns
        assert 'home_offensive_rating' in result.columns
        assert 'away_offensive_rating' in result.columns


# ====================
# Tests for _aggregate_player_stats()
# ====================

class TestAggregatePlayerStats:
    """Tests for player stats aggregation function."""
    
    def test_aggregates_to_star_player(self):
        """
        Test aggregation to star player (highest points).
        
        Should select the player with most points per game.
        """
        # Arrange: Player stats DataFrame with multiple players
        player_stats_df = pd.DataFrame([
            {'game_id': 'game_1', 'player_id': 'player_1', 'points': 28, 'rebounds': 8},
            {'game_id': 'game_1', 'player_id': 'player_2', 'points': 22, 'rebounds': 10}
        ])
        
        # Act: Aggregate
        result = _aggregate_player_stats(player_stats_df)
        
        # Assert: Should select highest scoring player
        assert len(result) == 1
        assert result.iloc[0]['game_id'] == 'game_1'


# ====================
# Tests for add_target_columns()
# ====================

class TestAddTargetColumns:
    """Tests for target column creation."""
    
    def test_adds_all_target_columns(self, sample_dataframe):
        """
        Test that all 7 target columns are added.
        
        Acceptance Criteria: Creates targets for all model types
        """
        # Act: Add target columns
        df = add_target_columns(sample_dataframe)
        
        # Assert: All targets should exist
        expected_targets = [
            'home_win',
            'home_cover',
            'total_over',
            'player_points_over_25',
            'player_rebounds_over_10',
            'overtime_yes',
            'first_team_to_20_home'
        ]
        
        for target in expected_targets:
            assert target in df.columns, f"Should have {target} column"
    
    def test_home_win_calculation(self):
        """
        Test home_win target calculation.
        
        home_win = 1 if home_final_score > away_final_score
        """
        # Arrange: Create test data with known outcomes
        df = pd.DataFrame({
            'home_final_score': [120, 100, 105],
            'away_final_score': [110, 105, 105],  # Win, Loss, Tie
            'spread_line': [0, 0, 0],
            'total_line': [200, 200, 200],
            'overtime_occurred': [0, 0, 0],
            'first_to_20_was_home': [0, 0, 0],
            'player_actual_points': [20, 20, 20],
            'player_actual_rebounds': [10, 10, 10]
        })
        
        # Act: Add targets
        df = add_target_columns(df)
        
        # Assert: Check home_win values
        assert df['home_win'].iloc[0] == 1, "120 > 110, should be win"
        assert df['home_win'].iloc[1] == 0, "100 < 105, should be loss"
        assert df['home_win'].iloc[2] == 0, "105 = 105, should be loss (not win)"


# ====================
# Tests for validate_training_data()
# ====================

class TestValidateTrainingData:
    """Tests for data validation."""
    
    def test_validates_correct_data(self, sample_dataframe):
        """
        Test that valid data passes validation.
        """
        # Act & Assert: Should not raise error
        try:
            if len(sample_dataframe) < 100:
                n_samples = 100 - len(sample_dataframe) + 1  # Add enough samples to reach 100
                other_sample_dataframe = generate_synthetic_training_data(n_samples=n_samples)
                # If sample data has < 100 rows, we need to create a larger DataFrame
                sample_dataframe = pd.concat([sample_dataframe, other_sample_dataframe], ignore_index=True)

            validate_training_data(sample_dataframe)
        except ValueError:
            pytest.fail("Valid data should not raise ValueError")
    
    def test_raises_error_for_missing_required_columns(self):
        """
        Test that missing required columns raises ValueError.
        """
        # Arrange: DataFrame missing required column
        df = pd.DataFrame({
            'game_id': [1, 2, 3],
            'home_offensive_rating': [115.0, 110.0, 112.0]
            # Missing: away_offensive_rating, home_defensive_rating, etc.
        })
        
        # Act & Assert: Should raise ValueError
        with pytest.raises(ValueError, match="Missing required columns"):
            validate_training_data(df)
    
    def test_raises_error_for_insufficient_data(self):
        """
        Test that DataFrames with < 100 rows raise ValueError.
        
        Minimum requirement is 100 samples for training.
        """
        # Arrange: DataFrame with only 50 rows
        df = pd.DataFrame({
            'home_offensive_rating': np.random.uniform(100, 120, 50),
            'away_offensive_rating': np.random.uniform(100, 120, 50),
            'home_defensive_rating': np.random.uniform(100, 120, 50),
            'away_defensive_rating': np.random.uniform(100, 120, 50)
        })
        
        # Act & Assert: Should raise ValueError
        with pytest.raises(ValueError, match="Insufficient training data"):
            validate_training_data(df)


# ====================
# Integration Tests
# ====================

class TestDataLoaderIntegration:
    """Integration tests for complete data loading workflows."""
    
    def test_end_to_end_synthetic_pipeline(self):
        """
        Test complete pipeline: load synthetic → validate → targets.
        
        This simulates the actual usage pattern in training.
        """
        # Act: Load data (internally adds targets)
        df = load_training_data(source="synthetic")
        
        # Assert: Should be valid and ready for training
        validate_training_data(df)  # Should not raise
        
        # Check targets exist
        assert 'home_win' in df.columns
        assert 'home_cover' in df.columns
        
        # Check data quality
        assert len(df) > 0
        assert df.isnull().sum().sum() == 0  # No nulls
    
    @patch('requests.get')
    def test_end_to_end_moneyball_pipeline(
        self,
        mock_get,
        mock_games_response,
        mock_team_stats_response
    ):
        """
        Test complete Moneyball pipeline: API → join → targets.
        
        This simulates actual usage with Moneyball API.
        """
        # Arrange: Mock all API responses
        def side_effect(url, **kwargs):
            response = Mock()
            response.status_code = 200
            
            if 'games' in url:
                # Return games with all required fields for targets
                games_with_outcomes = [
                    {
                        **game,
                        'spread_line': -5.5,
                        'total_line': 225.5,
                        'overtime_occurred': 0,
                        'first_to_20_was_home': 1,
                        'player_actual_points': 28.0,
                        'player_actual_rebounds': 12.0
                    }
                    for game in mock_games_response
                ]
                response.json.return_value = games_with_outcomes
            elif 'teams' in url:
                response.json.return_value = mock_team_stats_response
            else:
                response.json.return_value = []
            
            return response
        
        mock_get.side_effect = side_effect
        
        # Act: Load from Moneyball (with targets added)
        df = load_training_data(
            source="moneyball",
            start_date='2024-01-01',
            end_date='2024-01-31'
        )
        
        # Assert: Should have complete data with targets
        assert isinstance(df, pd.DataFrame)
        assert len(df) > 0
        assert 'home_win' in df.columns  # Targets should be added


if __name__ == "__main__":
    pytest.main([__file__, "-v"])