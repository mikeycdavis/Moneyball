"""
Unit tests for model_config.py

Tests the model configuration module:
- ModelType enum - Available model types
- FeatureGroup enum - Available feature groups
- ModelConfig dataclass - Model configuration structure
- MODEL_CONFIGS - List of all model configurations
- get_all_model_configs() - Get all configurations
- get_model_config() - Get specific configuration
- get_feature_groups_for_model() - Get feature groups

Test Structure:
- Test enum definitions
- Test dataclass structure
- Test model configurations
- Test helper functions
- Test acceptance criteria compliance
"""

import pytest
from typing import List

# Import the module to test
from moneyball_ml_python.training.model_config import (
    ModelType,
    FeatureGroup,
    ModelConfig,
    MODEL_CONFIGS,
    get_all_model_configs,
    get_model_config,
    get_feature_groups_for_model
)


# ====================
# Tests for ModelType Enum
# ====================

class TestModelType:
    """Tests for ModelType enum."""
    
    def test_has_logistic_regression(self):
        """Test that LOGISTIC_REGRESSION model type exists."""
        assert hasattr(ModelType, 'LOGISTIC_REGRESSION')
        assert ModelType.LOGISTIC_REGRESSION.value == "logistic_regression"
    
    def test_has_random_forest(self):
        """Test that RANDOM_FOREST model type exists."""
        assert hasattr(ModelType, 'RANDOM_FOREST')
        assert ModelType.RANDOM_FOREST.value == "random_forest"
    
    def test_has_xgboost(self):
        """Test that XGBOOST model type exists."""
        assert hasattr(ModelType, 'XGBOOST')
        assert ModelType.XGBOOST.value == "xgboost"
    
    def test_has_lightgbm(self):
        """Test that LIGHTGBM model type exists."""
        assert hasattr(ModelType, 'LIGHTGBM')
        assert ModelType.LIGHTGBM.value == "lightgbm"
    
    def test_all_model_types_have_string_values(self):
        """Test that all model types have string values."""
        for model_type in ModelType:
            assert isinstance(model_type.value, str)
            assert len(model_type.value) > 0
    
    def test_model_type_comparison(self):
        """Test that model types can be compared."""
        assert ModelType.LOGISTIC_REGRESSION == ModelType.LOGISTIC_REGRESSION
        assert ModelType.LOGISTIC_REGRESSION != ModelType.RANDOM_FOREST


# ====================
# Tests for FeatureGroup Enum
# ====================

class TestFeatureGroup:
    """Tests for FeatureGroup enum."""
    
    def test_has_team_stats(self):
        """Test that TEAM_STATS feature group exists."""
        assert hasattr(FeatureGroup, 'TEAM_STATS')
        assert FeatureGroup.TEAM_STATS.value == "team_stats"
    
    def test_has_player_stats(self):
        """Test that PLAYER_STATS feature group exists."""
        assert hasattr(FeatureGroup, 'PLAYER_STATS')
        assert FeatureGroup.PLAYER_STATS.value == "player_stats"
    
    def test_has_betting_lines(self):
        """Test that BETTING_LINES feature group exists."""
        assert hasattr(FeatureGroup, 'BETTING_LINES')
        assert FeatureGroup.BETTING_LINES.value == "betting_lines"
    
    def test_has_derived(self):
        """Test that DERIVED feature group exists."""
        assert hasattr(FeatureGroup, 'DERIVED')
        assert FeatureGroup.DERIVED.value == "derived"
    
    def test_has_matchup(self):
        """Test that MATCHUP feature group exists."""
        assert hasattr(FeatureGroup, 'MATCHUP')
        assert FeatureGroup.MATCHUP.value == "matchup"
    
    def test_all_feature_groups_have_string_values(self):
        """Test that all feature groups have string values."""
        for feature_group in FeatureGroup:
            assert isinstance(feature_group.value, str)
            assert len(feature_group.value) > 0


# ====================
# Tests for ModelConfig Dataclass
# ====================

class TestModelConfig:
    """Tests for ModelConfig dataclass."""
    
    def test_can_create_model_config(self):
        """Test that ModelConfig can be instantiated."""
        # Arrange & Act: Create model config
        config = ModelConfig(
            name="test_model",
            target="test_target",
            model_type=ModelType.LOGISTIC_REGRESSION,
            feature_groups=[FeatureGroup.TEAM_STATS],
            description="Test model",
            hyperparameters={"param": 1}
        )
        
        # Assert: Check all fields
        assert config.name == "test_model"
        assert config.target == "test_target"
        assert config.model_type == ModelType.LOGISTIC_REGRESSION
        assert config.feature_groups == [FeatureGroup.TEAM_STATS]
        assert config.description == "Test model"
        assert config.hyperparameters == {"param": 1}
    
    def test_hyperparameters_optional(self):
        """Test that hyperparameters field is optional."""
        # Arrange & Act: Create without hyperparameters
        config = ModelConfig(
            name="test_model",
            target="test_target",
            model_type=ModelType.RANDOM_FOREST,
            feature_groups=[FeatureGroup.TEAM_STATS],
            description="Test"
        )
        
        # Assert: hyperparameters should be None
        assert config.hyperparameters is None
    
    def test_feature_groups_is_list(self):
        """Test that feature_groups accepts a list."""
        # Arrange & Act: Create with multiple feature groups
        config = ModelConfig(
            name="test",
            target="target",
            model_type=ModelType.XGBOOST,
            feature_groups=[
                FeatureGroup.TEAM_STATS,
                FeatureGroup.PLAYER_STATS,
                FeatureGroup.BETTING_LINES
            ],
            description="Test"
        )
        
        # Assert: Should have all feature groups
        assert len(config.feature_groups) == 3
        assert FeatureGroup.TEAM_STATS in config.feature_groups
        assert FeatureGroup.PLAYER_STATS in config.feature_groups
        assert FeatureGroup.BETTING_LINES in config.feature_groups


# ====================
# Tests for MODEL_CONFIGS
# ====================

class TestModelConfigs:
    """
    Tests for MODEL_CONFIGS list.
    
    Acceptance Criteria: Trains all models in one run.
    """
    
    def test_model_configs_is_list(self):
        """Test that MODEL_CONFIGS is a list."""
        assert isinstance(MODEL_CONFIGS, list)
    
    def test_has_seven_models(self):
        """
        Test that there are 7 models defined.
        
        Acceptance Criteria: Trains all supported models:
        - Win/Loss (1)
        - Point Spread (1)
        - Over/Under (1)
        - Player Props (2)
        - Event Outcomes (2)
        Total: 7 models
        """
        assert len(MODEL_CONFIGS) == 7, "Should have 7 model configurations"
    
    def test_has_home_win_model(self):
        """
        Test that home_win (Win/Loss) model exists.
        
        Acceptance Criteria: Trains Win/Loss model.
        """
        names = [config.name for config in MODEL_CONFIGS]
        assert "home_win" in names, "Should have home_win model"
    
    def test_has_home_cover_model(self):
        """
        Test that home_cover (Point Spread) model exists.
        
        Acceptance Criteria: Trains Point Spread model.
        """
        names = [config.name for config in MODEL_CONFIGS]
        assert "home_cover" in names, "Should have home_cover model"
    
    def test_has_total_over_model(self):
        """
        Test that total_over (Over/Under) model exists.
        
        Acceptance Criteria: Trains Over/Under model.
        """
        names = [config.name for config in MODEL_CONFIGS]
        assert "total_over" in names, "Should have total_over model"
    
    def test_has_player_prop_models(self):
        """
        Test that player prop models exist.
        
        Acceptance Criteria: Trains player-specific outcome models.
        """
        names = [config.name for config in MODEL_CONFIGS]
        assert "player_points_over_25" in names
        assert "player_rebounds_over_10" in names
    
    def test_has_event_outcome_models(self):
        """
        Test that event outcome models exist.
        
        Acceptance Criteria: Trains prop/event outcome models.
        """
        names = [config.name for config in MODEL_CONFIGS]
        assert "overtime_yes" in names
        assert "first_team_to_20" in names
    
    def test_all_configs_are_model_config_instances(self):
        """Test that all configs are ModelConfig instances."""
        for config in MODEL_CONFIGS:
            assert isinstance(config, ModelConfig)
    
    def test_all_models_have_unique_names(self):
        """Test that all model names are unique."""
        names = [config.name for config in MODEL_CONFIGS]
        assert len(names) == len(set(names)), "Model names should be unique"
    
    def test_all_models_have_names(self):
        """Test that all models have non-empty names."""
        for config in MODEL_CONFIGS:
            assert config.name is not None
            assert len(config.name) > 0
    
    def test_all_models_have_targets(self):
        """Test that all models have target columns."""
        for config in MODEL_CONFIGS:
            assert config.target is not None
            assert len(config.target) > 0
    
    def test_all_models_have_model_types(self):
        """Test that all models have valid model types."""
        for config in MODEL_CONFIGS:
            assert config.model_type is not None
            assert isinstance(config.model_type, ModelType)
    
    def test_all_models_have_feature_groups(self):
        """Test that all models have at least one feature group."""
        for config in MODEL_CONFIGS:
            assert config.feature_groups is not None
            assert len(config.feature_groups) > 0
            # Check all are FeatureGroup enums
            for fg in config.feature_groups:
                assert isinstance(fg, FeatureGroup)
    
    def test_all_models_have_descriptions(self):
        """Test that all models have descriptions."""
        for config in MODEL_CONFIGS:
            assert config.description is not None
            assert len(config.description) > 0
    
    def test_all_models_have_hyperparameters(self):
        """Test that all models have hyperparameters defined."""
        for config in MODEL_CONFIGS:
            assert config.hyperparameters is not None
            assert isinstance(config.hyperparameters, dict)
            assert len(config.hyperparameters) > 0


# ====================
# Tests for Specific Model Configurations
# ====================

class TestSpecificModelConfigs:
    """Tests for specific model configurations."""
    
    def test_home_win_configuration(self):
        """
        Test home_win model configuration details.
        
        Should use logistic regression with team stats, derived, and matchup features.
        """
        # Arrange: Get home_win config
        config = get_model_config("home_win")
        
        # Assert: Check configuration
        assert config is not None
        assert config.name == "home_win"
        assert config.target == "home_win"
        assert config.model_type == ModelType.LOGISTIC_REGRESSION
        assert FeatureGroup.TEAM_STATS in config.feature_groups
        assert FeatureGroup.DERIVED in config.feature_groups
        assert FeatureGroup.MATCHUP in config.feature_groups
        assert "home team winning" in config.description.lower()
    
    def test_home_cover_configuration(self):
        """
        Test home_cover model configuration details.
        
        Should use random forest with team stats, betting lines, derived, and matchup.
        """
        # Arrange: Get home_cover config
        config = get_model_config("home_cover")
        
        # Assert: Check configuration
        assert config is not None
        assert config.name == "home_cover"
        assert config.target == "home_cover"
        assert config.model_type == ModelType.RANDOM_FOREST
        assert FeatureGroup.TEAM_STATS in config.feature_groups
        assert FeatureGroup.BETTING_LINES in config.feature_groups
        assert FeatureGroup.DERIVED in config.feature_groups
        assert FeatureGroup.MATCHUP in config.feature_groups
        assert "spread" in config.description.lower()
    
    def test_total_over_configuration(self):
        """Test total_over model configuration details."""
        # Arrange: Get total_over config
        config = get_model_config("total_over")
        
        # Assert: Check configuration
        assert config is not None
        assert config.name == "total_over"
        assert config.target == "total_over"
        assert config.model_type == ModelType.LOGISTIC_REGRESSION
        assert FeatureGroup.BETTING_LINES in config.feature_groups
        assert "over" in config.description.lower()
    
    def test_player_prop_configurations(self):
        """Test player prop model configurations."""
        # Arrange: Get player prop configs
        points_config = get_model_config("player_points_over_25")
        rebounds_config = get_model_config("player_rebounds_over_10")
        
        # Assert: Check both configs
        assert points_config is not None
        assert rebounds_config is not None
        
        # Points model should have player stats
        assert FeatureGroup.PLAYER_STATS in points_config.feature_groups
        assert "25 points" in points_config.description
        
        # Rebounds model should have player stats
        assert FeatureGroup.PLAYER_STATS in rebounds_config.feature_groups
        assert "10 rebounds" in rebounds_config.description
    
    def test_event_outcome_configurations(self):
        """Test event outcome model configurations."""
        # Arrange: Get event outcome configs
        overtime_config = get_model_config("overtime_yes")
        first_to_20_config = get_model_config("first_team_to_20")
        
        # Assert: Check both configs
        assert overtime_config is not None
        assert first_to_20_config is not None
        
        assert "overtime" in overtime_config.description.lower()
        assert "20 points" in first_to_20_config.description.lower()


# ====================
# Tests for get_all_model_configs()
# ====================

class TestGetAllModelConfigs:
    """Tests for get_all_model_configs() function."""
    
    def test_returns_list(self):
        """Test that function returns a list."""
        result = get_all_model_configs()
        assert isinstance(result, list)
    
    def test_returns_all_configs(self):
        """Test that all 7 configs are returned."""
        result = get_all_model_configs()
        assert len(result) == 7
    
    def test_returns_model_config_instances(self):
        """Test that all returned items are ModelConfig instances."""
        result = get_all_model_configs()
        for config in result:
            assert isinstance(config, ModelConfig)
    
    def test_returns_same_as_model_configs(self):
        """Test that returned list matches MODEL_CONFIGS."""
        result = get_all_model_configs()
        assert result == MODEL_CONFIGS


# ====================
# Tests for get_model_config()
# ====================

class TestGetModelConfig:
    """Tests for get_model_config() function."""
    
    def test_returns_config_for_valid_name(self):
        """Test that valid model name returns config."""
        # Act: Get config
        config = get_model_config("home_win")
        
        # Assert: Should return config
        assert config is not None
        assert isinstance(config, ModelConfig)
        assert config.name == "home_win"
    
    def test_returns_none_for_invalid_name(self):
        """Test that invalid model name returns None."""
        # Act: Get config for non-existent model
        config = get_model_config("nonexistent_model")
        
        # Assert: Should return None
        assert config is None
    
    def test_works_for_all_model_names(self):
        """Test that all model names can be retrieved."""
        # Arrange: Get all model names
        expected_names = [
            "home_win",
            "home_cover",
            "total_over",
            "player_points_over_25",
            "player_rebounds_over_10",
            "overtime_yes",
            "first_team_to_20"
        ]
        
        # Act & Assert: Each should return a config
        for name in expected_names:
            config = get_model_config(name)
            assert config is not None
            assert config.name == name
    
    def test_case_sensitive(self):
        """Test that model name lookup is case-sensitive."""
        # Act: Try with different case
        config = get_model_config("HOME_WIN")  # Wrong case
        
        # Assert: Should not find it
        assert config is None
    
    def test_returns_correct_config_details(self):
        """Test that returned config has correct details."""
        # Act: Get specific config
        config = get_model_config("home_cover")
        
        # Assert: Check all details
        assert config.name == "home_cover"
        assert config.target == "home_cover"
        assert config.model_type == ModelType.RANDOM_FOREST
        assert len(config.feature_groups) > 0
        assert config.description is not None


# ====================
# Tests for get_feature_groups_for_model()
# ====================

class TestGetFeatureGroupsForModel:
    """Tests for get_feature_groups_for_model() function."""
    
    def test_returns_list(self):
        """Test that function returns a list."""
        result = get_feature_groups_for_model("home_win")
        assert isinstance(result, list)
    
    def test_returns_feature_groups_for_valid_model(self):
        """Test that valid model name returns feature groups."""
        # Act: Get feature groups
        groups = get_feature_groups_for_model("home_win")
        
        # Assert: Should return non-empty list
        assert len(groups) > 0
        # All should be FeatureGroup enums
        for group in groups:
            assert isinstance(group, FeatureGroup)
    
    def test_returns_empty_list_for_invalid_model(self):
        """Test that invalid model name returns empty list."""
        # Act: Get feature groups for non-existent model
        groups = get_feature_groups_for_model("nonexistent_model")
        
        # Assert: Should return empty list
        assert groups == []
    
    def test_returns_correct_groups_for_home_win(self):
        """Test that home_win returns correct feature groups."""
        # Act: Get feature groups
        groups = get_feature_groups_for_model("home_win")
        
        # Assert: Should have specific groups
        assert FeatureGroup.TEAM_STATS in groups
        assert FeatureGroup.DERIVED in groups
        assert FeatureGroup.MATCHUP in groups
    
    def test_returns_correct_groups_for_home_cover(self):
        """Test that home_cover returns correct feature groups."""
        # Act: Get feature groups
        groups = get_feature_groups_for_model("home_cover")
        
        # Assert: Should include betting lines
        assert FeatureGroup.TEAM_STATS in groups
        assert FeatureGroup.BETTING_LINES in groups
        assert FeatureGroup.DERIVED in groups
        assert FeatureGroup.MATCHUP in groups
    
    def test_returns_correct_groups_for_player_model(self):
        """Test that player prop model returns correct groups."""
        # Act: Get feature groups
        groups = get_feature_groups_for_model("player_points_over_25")
        
        # Assert: Should include player stats
        assert FeatureGroup.PLAYER_STATS in groups


# ====================
# Integration Tests
# ====================

class TestModelConfigIntegration:
    """Integration tests for model configuration system."""
    
    def test_all_models_can_be_retrieved_and_used(self):
        """
        Test that all models can be retrieved and their configs used.
        
        This simulates the training pipeline usage.
        """
        # Arrange: Get all configs
        all_configs = get_all_model_configs()
        
        # Act & Assert: Process each config
        for config in all_configs:
            # Should be able to get by name
            retrieved_config = get_model_config(config.name)
            assert retrieved_config == config
            
            # Should be able to get feature groups
            feature_groups = get_feature_groups_for_model(config.name)
            assert feature_groups == config.feature_groups
            
            # Config should have all required fields
            assert config.name is not None
            assert config.target is not None
            assert config.model_type is not None
            assert len(config.feature_groups) > 0
            assert config.description is not None
    
    def test_model_types_are_valid_for_training(self):
        """
        Test that all model types are valid sklearn/xgboost/lightgbm types.
        """
        # Arrange: Get all configs
        all_configs = get_all_model_configs()
        
        # Act & Assert: Check model types
        valid_types = [
            ModelType.LOGISTIC_REGRESSION,
            ModelType.RANDOM_FOREST,
            ModelType.XGBOOST,
            ModelType.LIGHTGBM
        ]
        
        for config in all_configs:
            assert config.model_type in valid_types


# ====================
# Acceptance Criteria Tests
# ====================

class TestAcceptanceCriteria:
    """Tests specifically for acceptance criteria."""
    
    def test_trains_all_model_types(self):
        """
        Test that all required model types are present.
        
        Acceptance Criteria: Trains all of the following:
        - Win/Loss model (home_win) ✓
        - Point Spread model (home_cover) ✓
        - Over/Under model (total_over) ✓
        - Player-specific outcome models ✓
        - Prop/Event outcome models ✓
        """
        configs = get_all_model_configs()
        names = [c.name for c in configs]
        
        # Win/Loss
        assert "home_win" in names
        
        # Point Spread
        assert "home_cover" in names
        
        # Over/Under
        assert "total_over" in names
        
        # Player props
        player_prop_models = [n for n in names if "player" in n]
        assert len(player_prop_models) >= 2
        
        # Event outcomes
        event_models = [n for n in names if n in ["overtime_yes", "first_team_to_20"]]
        assert len(event_models) >= 2
    
    def test_single_execution_trains_all(self):
        """
        Test that MODEL_CONFIGS contains all models for single execution.
        
        Acceptance Criteria: A single execution of train.py trains all models.
        """
        configs = get_all_model_configs()
        
        # Should have all 7 models
        assert len(configs) == 7
        
        # All should be accessible
        for config in configs:
            assert get_model_config(config.name) is not None


if __name__ == "__main__":
    pytest.main([__file__, "-v"])