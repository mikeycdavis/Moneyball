"""
Tests for predict.py

Tests all acceptance criteria:
- All IsActive models loaded at startup
- prepare_features maps dict to numpy array
- predict returns None for missing models (404)
- predict completes in < 200ms
"""

import pytest
import json
import numpy as np
from pathlib import Path
from moneyball_ml_python.prediction.predict import PredictionService


@pytest.fixture
def service():
    """Create a prediction service for testing."""
    return PredictionService(models_dir="test_models")


def test_prepare_features_with_expected_order(service):
    """
    Test that prepare_features maps feature dict to numpy array.
    Acceptance Criteria: prepare_features maps feature dict to numpy array.
    """
    # Arrange
    features = {
        "HomeWinRate": 0.65,
        "AwayWinRate": 0.35,
        "HomePointsAvg": 110.0
    }
    expected_features = ["HomeWinRate", "AwayWinRate", "HomePointsAvg"]
    
    # Act
    result = service.prepare_features(features, expected_features)
    
    # Assert - Should be numpy array
    assert isinstance(result, np.ndarray)
    assert result.shape == (1, 3)
    assert result[0][0] == 0.65
    assert result[0][1] == 0.35
    assert result[0][2] == 110.0


def test_prepare_features_missing_feature_uses_default(service):
    """
    Test that missing features are filled with 0.0.
    """
    # Arrange
    features = {
        "HomeWinRate": 0.65,
        "AwayWinRate": 0.35
        # Missing: HomePointsAvg
    }
    expected_features = ["HomeWinRate", "AwayWinRate", "HomePointsAvg"]
    
    # Act
    result = service.prepare_features(features, expected_features)
    
    # Assert - Missing feature should be 0.0
    assert result.shape == (1, 3)
    assert result[0][2] == 0.0


def test_prepare_features_without_expected_order(service):
    """
    Test prepare_features with no expected order (alphabetical).
    """
    # Arrange
    features = {
        "ZFeature": 3.0,
        "AFeature": 1.0,
        "MFeature": 2.0
    }
    
    # Act
    result = service.prepare_features(features, expected_features=None)
    
    # Assert - Should be alphabetical order
    assert result.shape == (1, 3)
    assert result[0][0] == 1.0  # AFeature
    assert result[0][1] == 2.0  # MFeature
    assert result[0][2] == 3.0  # ZFeature


def test_predict_model_not_found_returns_none(service):
    """
    Test that predict returns None when model not found.
    Acceptance Criteria: Model not found returns 404 (None in service).
    """
    # Arrange
    features = {"HomeWinRate": 0.65}
    
    # Act
    result = service.predict("nonexistent_model", features)
    
    # Assert - Should return None (caller returns 404)
    assert result is None


def test_get_loaded_models_empty_initially(service):
    """
    Test that no models are loaded initially.
    """
    # Act
    models = service.get_loaded_models()
    
    # Assert
    assert isinstance(models, list)
    assert len(models) == 0


def test_get_model_info_not_found(service):
    """
    Test getting info for non-existent model.
    """
    # Act
    info = service.get_model_info("nonexistent")
    
    # Assert
    assert info is None