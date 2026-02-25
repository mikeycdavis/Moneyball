"""
Model configuration for all betting models.

Defines all models trained in a single train.py execution.
Each model has:
- name: Unique identifier
- target: Column name in training data
- model_type: Type of model to use
- features: Which feature groups to use
- description: What the model predicts
"""

from dataclasses import dataclass
from typing import List, Optional
from enum import Enum


class ModelType(Enum):
    """Types of ML models available."""
    LOGISTIC_REGRESSION = "logistic_regression"
    RANDOM_FOREST = "random_forest"
    XGBOOST = "xgboost"
    LIGHTGBM = "lightgbm"


class FeatureGroup(Enum):
    """Feature groups for different model types."""
    TEAM_STATS = "team_stats"
    PLAYER_STATS = "player_stats"
    BETTING_LINES = "betting_lines"
    DERIVED = "derived"
    MATCHUP = "matchup"


@dataclass
class ModelConfig:
    """Configuration for a single model."""
    name: str
    target: str
    model_type: ModelType
    feature_groups: List[FeatureGroup]
    description: str
    hyperparameters: Optional[dict] = None


# All models to train (Acceptance Criteria: trains all in one run)
MODEL_CONFIGS = [
    # ==================== Win/Loss Model ====================
    ModelConfig(
        name="home_win",
        target="home_win",
        model_type=ModelType.LOGISTIC_REGRESSION,
        feature_groups=[
            FeatureGroup.TEAM_STATS,
            FeatureGroup.DERIVED,
            FeatureGroup.MATCHUP
        ],
        description="Predicts probability of home team winning the game",
        hyperparameters={"max_iter": 1000, "random_state": 42}
    ),
    
    # ==================== Point Spread Model ====================
    ModelConfig(
        name="home_cover",
        target="home_cover",
        model_type=ModelType.RANDOM_FOREST,
        feature_groups=[
            FeatureGroup.TEAM_STATS,
            FeatureGroup.BETTING_LINES,
            FeatureGroup.DERIVED,
            FeatureGroup.MATCHUP
        ],
        description="Predicts probability of home team covering the spread",
        hyperparameters={"n_estimators": 100, "max_depth": 10, "random_state": 42}
    ),
    
    # ==================== Over/Under Model ====================
    ModelConfig(
        name="total_over",
        target="total_over",
        model_type=ModelType.LOGISTIC_REGRESSION,
        feature_groups=[
            FeatureGroup.TEAM_STATS,
            FeatureGroup.BETTING_LINES,
            FeatureGroup.DERIVED
        ],
        description="Predicts probability of total points going over the line",
        hyperparameters={"max_iter": 1000, "random_state": 42}
    ),
    
    # ==================== Player Prop Models ====================
    ModelConfig(
        name="player_points_over_25",
        target="player_points_over_25",
        model_type=ModelType.RANDOM_FOREST,
        feature_groups=[
            FeatureGroup.PLAYER_STATS,
            FeatureGroup.MATCHUP,
            FeatureGroup.DERIVED
        ],
        description="Predicts probability of player scoring over 25 points",
        hyperparameters={"n_estimators": 100, "random_state": 42}
    ),
    
    ModelConfig(
        name="player_rebounds_over_10",
        target="player_rebounds_over_10",
        model_type=ModelType.LOGISTIC_REGRESSION,
        feature_groups=[
            FeatureGroup.PLAYER_STATS,
            FeatureGroup.MATCHUP
        ],
        description="Predicts probability of player getting over 10 rebounds",
        hyperparameters={"max_iter": 1000, "random_state": 42}
    ),
    
    # ==================== Prop/Event Outcome Models ====================
    ModelConfig(
        name="overtime_yes",
        target="overtime_yes",
        model_type=ModelType.LOGISTIC_REGRESSION,
        feature_groups=[
            FeatureGroup.TEAM_STATS,
            FeatureGroup.DERIVED
        ],
        description="Predicts probability of game going to overtime",
        hyperparameters={"max_iter": 1000, "random_state": 42}
    ),
    
    ModelConfig(
        name="first_team_to_20",
        target="first_team_to_20_home",
        model_type=ModelType.LOGISTIC_REGRESSION,
        feature_groups=[
            FeatureGroup.TEAM_STATS,
            FeatureGroup.DERIVED
        ],
        description="Predicts probability of home team scoring 20 points first",
        hyperparameters={"max_iter": 1000, "random_state": 42}
    ),
]


def get_all_model_configs() -> List[ModelConfig]:
    """
    Get all model configurations.
    
    Returns:
        List of all model configs to train
    """
    return MODEL_CONFIGS


def get_model_config(name: str) -> Optional[ModelConfig]:
    """
    Get configuration for a specific model.
    
    Args:
        name: Model name
        
    Returns:
        ModelConfig if found, None otherwise
    """
    for config in MODEL_CONFIGS:
        if config.name == name:
            return config
    return None


def get_feature_groups_for_model(model_name: str) -> List[FeatureGroup]:
    """
    Get feature groups required for a specific model.
    
    Args:
        model_name: Name of the model
        
    Returns:
        List of feature groups needed
    """
    config = get_model_config(model_name)
    if config:
        return config.feature_groups
    return []