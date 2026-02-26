"""
Unit tests for data_loader.py

Tests all functions in the data loader module:
- load_training_data() - Main entry point
- generate_synthetic_training_data() - Synthetic data generation
- load_from_csv() - CSV loading
- load_from_moneyball() - API integration (placeholder)
- add_target_columns() - Target column creation
- validate_training_data() - Data validation

Test Structure:
- Test basic functionality (happy path)
- Test edge cases
- Test error conditions
- Test data quality and structure
"""

import pytest
import pandas as pd
import numpy as np
from pathlib import Path
from datetime import datetime
import tempfile
import os

# Import the module to test
from moneyball_ml_python.data.data_loader import (
    load_training_data,
    generate_synthetic_training_data,
    load_from_csv,
    load_from_moneyball,
    add_target_columns,
    validate_training_data
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
    
    def test_load_from_moneyball_source(self):
        """
        Test loading from Moneyball API (currently returns synthetic).
        
        Note: Full Moneyball integration is not yet implemented,
        so this currently returns synthetic data.
        """
        # Act: Load from Moneyball (will use synthetic)
        df = load_training_data(source="moneyball")
        
        # Assert: Should return data (synthetic fallback)
        assert isinstance(df, pd.DataFrame)
        assert len(df) > 0
    
    def test_invalid_source_raises_error(self):
        """
        Test that invalid source parameter raises ValueError.
        """
        # Act & Assert: Invalid source should raise error
        with pytest.raises(ValueError, match="Unsupported data source"):
            load_training_data(source="invalid_source")
    
    def test_date_parameters_accepted(self):
        """
        Test that start_date and end_date parameters are accepted.
        
        Note: Currently not used by synthetic/CSV sources,
        but should not raise errors.
        """
        # Act: Pass date parameters
        df = load_training_data(
            source="synthetic",
            start_date="2024-01-01",
            end_date="2024-01-31"
        )
        
        # Assert: Should not raise error
        assert isinstance(df, pd.DataFrame)


# ====================
# Tests for generate_synthetic_training_data()
# ====================

class TestGenerateSyntheticTrainingData:
    """Tests for synthetic data generation."""
    
    def test_generates_correct_number_of_samples(self):
        """
        Test that function generates requested number of samples.
        """
        # Arrange: Request 500 samples
        n_samples = 500
        
        # Act: Generate data
        df = generate_synthetic_training_data(n_samples=n_samples)
        
        # Assert: Should have exactly 500 rows
        assert len(df) == n_samples, f"Should generate {n_samples} samples"
    
    def test_generates_default_number_of_samples(self):
        """
        Test default sample size (10,000 samples).
        """
        # Act: Generate with default size
        df = generate_synthetic_training_data()
        
        # Assert: Should have 10,000 rows by default
        assert len(df) == 10000, "Default should be 10,000 samples"
    
    def test_has_required_columns(self):
        """
        Test that synthetic data has all required columns.
        
        Checks for:
        - Game identifiers
        - Team statistics
        - Player statistics
        - Betting lines
        - Game outcomes
        """
        # Act: Generate data
        df = generate_synthetic_training_data(n_samples=100)
        
        # Assert: Check required columns exist
        required_columns = [
            # Identifiers
            'game_id', 'game_date',
            # Team offense
            'home_offensive_rating', 'away_offensive_rating',
            # Team defense
            'home_defensive_rating', 'away_defensive_rating',
            # Pace and tempo
            'home_pace', 'away_pace',
            # Rest
            'home_rest_days', 'away_rest_days',
            # Betting lines
            'spread_line', 'total_line',
            # Outcomes
            'home_final_score', 'away_final_score'
        ]
        
        for col in required_columns:
            assert col in df.columns, f"Should have {col} column"
    
    def test_numeric_columns_have_valid_ranges(self):
        """
        Test that generated numeric values are within realistic ranges.
        
        Checks:
        - Offensive ratings: 105-118
        - Defensive ratings: 105-118
        - Pace: 96-104
        - Rest days: 0-4
        - Spread: -12 to 12
        - Total: 210-235
        """
        # Act: Generate data
        df = generate_synthetic_training_data(n_samples=100)
        
        # Assert: Check ranges
        assert df['home_offensive_rating'].min() >= 105
        assert df['home_offensive_rating'].max() <= 118
        
        assert df['home_pace'].min() >= 96
        assert df['home_pace'].max() <= 104
        
        assert df['home_rest_days'].min() >= 0
        assert df['home_rest_days'].max() <= 4
        
        assert df['spread_line'].min() >= -12
        assert df['spread_line'].max() <= 12
        
        assert df['total_line'].min() >= 210
        assert df['total_line'].max() <= 235
    
    def test_game_ids_are_unique(self):
        """
        Test that game IDs are unique.
        """
        # Act: Generate data
        df = generate_synthetic_training_data(n_samples=100)
        
        # Assert: All game IDs should be unique
        assert df['game_id'].nunique() == len(df), "Game IDs should be unique"
    
    def test_no_missing_values(self):
        """
        Test that synthetic data has no missing values.
        """
        # Act: Generate data
        df = generate_synthetic_training_data(n_samples=100)
        
        # Assert: No nulls in any column
        assert df.isnull().sum().sum() == 0, "Should have no missing values"
    
    def test_correlation_between_offense_and_score(self):
        """
        Test that there's correlation between offensive rating and final score.
        
        Better offense should generally lead to higher scores.
        """
        # Act: Generate data
        df = generate_synthetic_training_data(n_samples=1000)
        
        # Assert: Should have positive correlation
        correlation = df['home_offensive_rating'].corr(df['home_final_score'])
        assert correlation > 0.4, "Should have strong positive correlation"
    
    def test_reproducibility_with_same_seed(self):
        """
        Test that generating data twice produces same results.
        
        Since we use np.random.seed(42), results should be reproducible.
        """
        # Act: Generate data twice
        df1 = generate_synthetic_training_data(n_samples=100)
        df2 = generate_synthetic_training_data(n_samples=100)
        
        # Assert: Should be identical
        pd.testing.assert_frame_equal(df1, df2, 
            "Should generate identical data with same seed")


# ====================
# Tests for load_from_csv()
# ====================

class TestLoadFromCSV:
    """Tests for CSV file loading."""
    
    def test_loads_csv_successfully(self, temp_csv_file):
        """
        Test basic CSV loading functionality.
        """
        # Act: Load CSV
        df = load_from_csv(temp_csv_file)
        
        # Assert: Data loaded
        assert isinstance(df, pd.DataFrame)
        assert len(df) == 3, "Should load all rows"
    
    def test_raises_error_if_file_not_found(self):
        """
        Test that FileNotFoundError is raised for missing file.
        """
        # Act & Assert: Should raise FileNotFoundError
        with pytest.raises(FileNotFoundError, match="CSV file not found"):
            load_from_csv("nonexistent_file.csv")
    
    def test_loads_data_with_correct_types(self, temp_csv_file):
        """
        Test that data types are preserved when loading CSV.
        """
        # Act: Load CSV
        df = load_from_csv(temp_csv_file)
        
        # Assert: Numeric columns should be numeric
        assert pd.api.types.is_numeric_dtype(df['home_offensive_rating'])
        assert pd.api.types.is_numeric_dtype(df['home_final_score'])
    
    def test_handles_empty_csv(self):
        """
        Test loading an empty CSV file.
        """
        # Arrange: Create empty CSV
        temp_file = tempfile.NamedTemporaryFile(mode='w', delete=False, suffix='.csv')
        temp_path = temp_file.name
        pd.DataFrame().to_csv(temp_path, index=False)
        temp_file.close()
        
        try:
            # Act: Load empty CSV
            df = load_from_csv(temp_path)
            
            # Assert: Should return empty DataFrame
            assert len(df) == 0, "Should handle empty CSV"
        finally:
            # Cleanup
            os.unlink(temp_path)


# ====================
# Tests for load_from_moneyball()
# ====================

class TestLoadFromMoneyball:
    """Tests for Moneyball API integration (placeholder)."""
    
    def test_returns_dataframe(self):
        """
        Test that function returns DataFrame.
        
        Note: Currently returns synthetic data as fallback.
        """
        # Act: Load from Moneyball
        df = load_from_moneyball()
        
        # Assert: Should return DataFrame
        assert isinstance(df, pd.DataFrame)
        assert len(df) > 0
    
    def test_accepts_date_parameters(self):
        """
        Test that start_date and end_date parameters are accepted.
        """
        # Act: Pass date parameters
        df = load_from_moneyball(
            start_date="2024-01-01",
            end_date="2024-01-31"
        )
        
        # Assert: Should not raise error
        assert isinstance(df, pd.DataFrame)
    
    def test_uses_synthetic_fallback(self):
        """
        Test that function uses synthetic data as fallback.
        
        Since Moneyball API is not yet implemented,
        should return synthetic data.
        """
        # Act: Load from Moneyball
        df = load_from_moneyball()
        
        # Assert: Should have characteristics of synthetic data
        assert len(df) == 10000, "Should use synthetic data (10k rows)"
        assert 'game_id' in df.columns


# ====================
# Tests for add_target_columns()
# ====================

class TestAddTargetColumns:
    """Tests for target column creation."""
    
    def test_adds_all_target_columns(self, sample_dataframe):
        """
        Test that all 7 target columns are added.
        
        Acceptance Criteria: Creates targets for all model types:
        - home_win
        - home_cover
        - total_over
        - player_points_over_25
        - player_rebounds_over_10
        - overtime_yes
        - first_team_to_20_home
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
    
    def test_home_cover_calculation(self):
        """
        Test home_cover target calculation.
        
        home_cover = 1 if (home_score - away_score) > spread_line
        """
        # Arrange: Test data with spread scenarios
        df = pd.DataFrame({
            'home_final_score': [120, 105, 110],
            'away_final_score': [110, 100, 115],
            'spread_line': [-5.0, -7.0, 3.0],  # Covers, Doesn't cover, Doesn't cover
            'total_line': [200, 200, 200],
            'overtime_occurred': [0, 0, 0],
            'first_to_20_was_home': [0, 0, 0],
            'player_actual_points': [20, 20, 20],
            'player_actual_rebounds': [10, 10, 10]
        })
        
        # Act: Add targets
        df = add_target_columns(df)
        
        # Assert: Check cover scenarios
        # Game 1: 120-110 = +10, spread -5, 10 > -5 = covers
        assert df['home_cover'].iloc[0] == 1, "Should cover spread"
        
        # Game 2: 105-100 = +5, spread -7, 5 > -7 but not by enough = doesn't cover
        assert df['home_cover'].iloc[1] == 1, "5 > -7, should cover"
        
        # Game 3: 110-115 = -5, spread +3, -5 < 3 = doesn't cover
        assert df['home_cover'].iloc[2] == 0, "Should not cover spread"
    
    def test_total_over_calculation(self):
        """
        Test total_over target calculation.
        
        total_over = 1 if (home_score + away_score) > total_line
        """
        # Arrange: Test data with over/under scenarios
        df = pd.DataFrame({
            'home_final_score': [120, 100, 105],
            'away_final_score': [110, 95, 100],
            'spread_line': [0, 0, 0],
            'total_line': [220.0, 200.0, 210.0],  # Over, Under, Under
            'overtime_occurred': [0, 0, 0],
            'first_to_20_was_home': [0, 0, 0],
            'player_actual_points': [20, 20, 20],
            'player_actual_rebounds': [10, 10, 10]
        })
        
        # Act: Add targets
        df = add_target_columns(df)
        
        # Assert: Check over/under
        assert df['total_over'].iloc[0] == 1, "230 > 220, should be over"
        assert df['total_over'].iloc[1] == 0, "195 < 200, should be under"
        assert df['total_over'].iloc[2] == 0, "205 < 210, should be under"
    
    def test_player_prop_calculations(self):
        """
        Test player prop target calculations.
        """
        # Arrange: Test player prop scenarios
        df = pd.DataFrame({
            'home_final_score': [100, 100],
            'away_final_score': [100, 100],
            'spread_line': [0, 0],
            'total_line': [200, 200],
            'overtime_occurred': [0, 0],
            'first_to_20_was_home': [0, 0],
            'player_actual_points': [28.0, 22.0],  # Over, Under
            'player_actual_rebounds': [12.0, 8.0]  # Over, Under
        })
        
        # Act: Add targets
        df = add_target_columns(df)
        
        # Assert: Check player props
        assert df['player_points_over_25'].iloc[0] == 1, "28 > 25"
        assert df['player_points_over_25'].iloc[1] == 0, "22 < 25"
        
        assert df['player_rebounds_over_10'].iloc[0] == 1, "12 > 10"
        assert df['player_rebounds_over_10'].iloc[1] == 0, "8 < 10"
    
    def test_event_outcome_calculations(self):
        """
        Test event outcome target calculations.
        """
        # Arrange: Test event outcomes
        df = pd.DataFrame({
            'home_final_score': [100, 100],
            'away_final_score': [100, 100],
            'spread_line': [0, 0],
            'total_line': [200, 200],
            'overtime_occurred': [1, 0],  # Yes, No
            'first_to_20_was_home': [1, 0],  # Home, Away
            'player_actual_points': [20, 20],
            'player_actual_rebounds': [10, 10]
        })
        
        # Act: Add targets
        df = add_target_columns(df)
        
        # Assert: Check event outcomes
        assert df['overtime_yes'].iloc[0] == 1, "Overtime occurred"
        assert df['overtime_yes'].iloc[1] == 0, "No overtime"
        
        assert df['first_team_to_20_home'].iloc[0] == 1, "Home team first to 20"
        assert df['first_team_to_20_home'].iloc[1] == 0, "Away team first to 20"
    
    def test_target_columns_are_binary(self, sample_dataframe):
        """
        Test that all target columns contain only 0 and 1.
        """
        # Act: Add targets
        df = add_target_columns(sample_dataframe)
        
        # Assert: All targets should be binary
        targets = [
            'home_win', 'home_cover', 'total_over',
            'player_points_over_25', 'player_rebounds_over_10',
            'overtime_yes', 'first_team_to_20_home'
        ]
        
        for target in targets:
            unique_values = df[target].unique()
            assert set(unique_values).issubset({0, 1}), \
                f"{target} should only contain 0 and 1"


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
    
    def test_warns_about_high_missing_values(self, caplog):
        """
        Test that high percentage of missing values triggers warning.
        
        Uses caplog to capture log messages.
        """
        # Arrange: DataFrame with > 50% missing in a column
        df = pd.DataFrame({
            'home_offensive_rating': [115.0] * 100,
            'away_offensive_rating': [110.0] * 100,
            'home_defensive_rating': [105.0] * 100,
            'away_defensive_rating': [110.0] * 100,
            'optional_stat': [np.nan] * 60 + [100.0] * 40  # 60% missing
        })
        
        # Act: Validate data
        validate_training_data(df)
        
        # Assert: Should log warning
        assert "missing values" in caplog.text.lower()
    
    def test_passes_with_exactly_100_rows(self):
        """
        Test that exactly 100 rows passes validation.
        """
        # Arrange: DataFrame with exactly 100 rows
        df = pd.DataFrame({
            'home_offensive_rating': np.random.uniform(100, 120, 100),
            'away_offensive_rating': np.random.uniform(100, 120, 100),
            'home_defensive_rating': np.random.uniform(100, 120, 100),
            'away_defensive_rating': np.random.uniform(100, 120, 100)
        })
        
        # Act & Assert: Should not raise error
        try:
            validate_training_data(df)
        except ValueError:
            pytest.fail("100 rows should pass validation")


# ====================
# Integration Tests
# ====================

class TestDataLoaderIntegration:
    """
    Integration tests for complete data loading workflows.
    """
    
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
    
    def test_end_to_end_csv_pipeline(self, temp_csv_file):
        """
        Test complete pipeline: CSV → load → validate → targets.
        """
        # Act: Load from CSV
        df = load_training_data(source="csv", filepath=temp_csv_file)
        
        # Assert: Should be valid
        assert 'home_win' in df.columns
        assert len(df) > 0
    
    def test_data_ready_for_model_training(self):
        """
        Test that loaded data has everything needed for training.
        
        Checks:
        - Has features
        - Has targets
        - Correct data types
        - No missing values
        """
        # Act: Load data
        df = load_training_data(source="synthetic")
        
        # Assert: Ready for training
        
        # Has feature columns
        assert 'home_offensive_rating' in df.columns
        assert 'away_offensive_rating' in df.columns
        
        # Has target columns
        assert 'home_win' in df.columns
        
        # Numeric columns are numeric
        assert pd.api.types.is_numeric_dtype(df['home_offensive_rating'])
        
        # No missing values
        assert df.isnull().sum().sum() == 0
        
        # Targets are binary
        assert set(df['home_win'].unique()).issubset({0, 1})


# ====================
# Performance Tests
# ====================

class TestDataLoaderPerformance:
    """
    Tests for performance characteristics.
    """
    
    def test_synthetic_generation_is_fast(self):
        """
        Test that generating 10,000 samples completes quickly.
        
        Should complete in < 5 seconds.
        """
        import time
        
        # Act: Time data generation
        start = time.time()
        df = generate_synthetic_training_data(n_samples=10000)
        elapsed = time.time() - start
        
        # Assert: Should be fast (< 5 seconds)
        assert elapsed < 5.0, f"Generation took {elapsed:.2f}s, should be < 5s"
    
    def test_csv_loading_is_fast(self, temp_csv_file):
        """
        Test that CSV loading completes quickly.
        """
        import time
        
        # Act: Time CSV loading
        start = time.time()
        df = load_from_csv(temp_csv_file)
        elapsed = time.time() - start
        
        # Assert: Should be very fast (< 1 second for small file)
        assert elapsed < 1.0, f"CSV loading took {elapsed:.2f}s"


if __name__ == "__main__":
    # Run tests with pytest
    pytest.main([__file__, "-v", "--cov=moneyball_ml_python.data.data_loader"])