"""
Unit tests for predict.py

Tests the PredictionService class:
- __init__() - Service initialization
- load_models() - Model loading at startup  
- predict() - Making predictions
- prepare_features() - Feature preparation
- get_loaded_models() - Listing models
- get_model_info() - Getting model metadata

Test Structure:
- Test happy path scenarios
- Test edge cases
- Test error conditions
- Test performance requirements (< 200ms)
- Test acceptance criteria compliance
"""

import pytest
import json
import tempfile
import time
from pathlib import Path
import numpy as np
import joblib
from unittest.mock import Mock, patch

# Import the class to test
from moneyball_ml_python.prediction.predict import PredictionService

class FakeModel:
    def predict_proba(self, X):
        return np.array([[0.35, 0.65]])

    def predict(self, X):
        return np.array([0.65])


# ====================
# Fixtures
# ====================

@pytest.fixture
def temp_models_dir():
    """
    Create a temporary models directory for testing.
    
    Creates a temporary directory that is cleaned up after the test.
    
    Yields:
        Path: Temporary directory path
    """
    temp_dir = tempfile.mkdtemp()
    yield Path(temp_dir)
    
    # Cleanup after test
    import shutil
    shutil.rmtree(temp_dir, ignore_errors=True)


@pytest.fixture
def mock_trained_model():
    """
    Create a mock trained model for testing.
    
    The mock model:
    - Has predict_proba method
    - Returns realistic probabilities
    - Simulates a real scikit-learn classifier
    
    Returns:
        Mock model object
    """    
    return FakeModel()


@pytest.fixture
def sample_model_file(temp_models_dir, mock_trained_model):
    """
    Create a sample model file (.pkl) for testing.
    
    Args:
        temp_models_dir: Temporary directory fixture
        mock_trained_model: Mock model fixture
        
    Returns:
        tuple: (model_path, metadata_path)
    """
    # Save mock model as .pkl file
    model_path = temp_models_dir / "test_model_v1.pkl"
    joblib.dump(mock_trained_model, model_path)
    
    # Create accompanying metadata file
    metadata = {
        "is_active": True,
        "expected_features": ["feature1", "feature2", "feature3"],
        "description": "Test model v1"
    }
    metadata_path = temp_models_dir / "test_model_v1.json"
    with open(metadata_path, 'w') as f:
        json.dump(metadata, f)
    
    return model_path, metadata_path


@pytest.fixture
def prediction_service(temp_models_dir):
    """
    Create a PredictionService instance for testing.
    
    Args:
        temp_models_dir: Temporary models directory
        
    Returns:
        PredictionService: Service instance
    """
    return PredictionService(models_dir=str(temp_models_dir))


# ====================
# Tests for __init__()
# ====================

class TestPredictionServiceInit:
    """Tests for service initialization."""
    
    def test_initializes_with_default_models_dir(self):
        """Test service initializes with default models directory."""
        # Act: Create service with defaults
        service = PredictionService()
        
        # Assert: Should use default directory
        assert service.models_dir == Path("models")
    
    def test_initializes_with_custom_models_dir(self):
        """Test service initializes with custom models directory."""
        # Act: Create service with custom directory
        service = PredictionService(models_dir="custom/path")
        
        # Assert: Should use custom directory
        assert service.models_dir == Path("custom/path")
    
    def test_initializes_empty_collections(self):
        """Test that loaded_models and model_metadata start empty."""
        # Act: Create service
        service = PredictionService()
        
        # Assert: Collections should be empty initially
        assert len(service.loaded_models) == 0
        assert len(service.model_metadata) == 0


# ====================
# Tests for load_models()
# ====================

class TestLoadModels:
    """Tests for model loading at startup."""
    
    def test_loads_active_model(self, prediction_service, sample_model_file):
        """
        Test that active models are loaded.
        
        Acceptance Criteria: All IsActive models loaded at startup.
        """
        # Act: Load models
        prediction_service.load_models()
        
        # Assert: Model should be loaded
        assert len(prediction_service.loaded_models) == 1
        assert "test_model_v1" in prediction_service.loaded_models
    
    def test_skips_inactive_model(self, temp_models_dir, mock_trained_model):
        """
        Test that inactive models are skipped.
        
        Acceptance Criteria: Only IsActive=True models are loaded.
        """
        # Arrange: Create inactive model
        model_path = temp_models_dir / "inactive_model.pkl"
        joblib.dump(mock_trained_model, model_path)
        
        metadata = {"is_active": False}  # Inactive!
        metadata_path = temp_models_dir / "inactive_model.json"
        with open(metadata_path, 'w') as f:
            json.dump(metadata, f)
        
        service = PredictionService(models_dir=str(temp_models_dir))
        
        # Act: Load models
        service.load_models()
        
        # Assert: Inactive model should be skipped
        assert "inactive_model" not in service.loaded_models
    
    def test_does_not_load_with_no_models(self):
        """
        Test that no models exist in model directory.
        
        Acceptance Criteria: All IsActive models loaded at startup.
        """
        # Act: Create service
        service = PredictionService(models_dir="custom/path")
        service.load_models()
        
        # Assert: Collections should be empty initially
        assert len(service.loaded_models) == 0
        assert len(service.model_metadata) == 0


# ====================
# Tests for predict()
# ====================

class TestPredict:
    """Tests for making predictions."""
    
    def test_returns_none_for_nonexistent_model(self, prediction_service):
        """
        Test that None is returned for non-existent model.
        
        Acceptance Criteria: Returns None if model not found (404).
        """
        # Act: Try to predict with non-existent model
        result = prediction_service.predict("nonexistent_model", {"feat": 1})
        
        # Assert: Should return None
        assert result is None
    
    def test_returns_probabilities(self, prediction_service, sample_model_file):
        """
        Test that prediction returns probability values.
        
        Acceptance Criteria: Returns probabilities.
        """
        # Arrange: Load model
        prediction_service.load_models()
        features = {"feature1": 0.5, "feature2": 0.7, "feature3": 0.3}
        
        # Act: Make prediction
        result = prediction_service.predict("test_model_v1", features)
        
        # Assert: Should have probability values
        assert 0 <= result["home_win_probability"] <= 1
        assert 0 <= result["away_win_probability"] <= 1
    
    def test_prediction_time_under_200ms(self, prediction_service, sample_model_file):
        """
        Test that predictions complete in < 200ms.
        
        Acceptance Criteria: Returns probabilities in < 200ms.
        """
        # Arrange: Load model
        prediction_service.load_models()
        features = {"feature1": 0.5, "feature2": 0.7, "feature3": 0.3}
        
        # Act: Make prediction
        result = prediction_service.predict("test_model_v1", features)
        
        # Assert: Should complete in < 200ms
        assert result["prediction_time_ms"] < 200


# ====================
# Tests for prepare_features()
# ====================

class TestPrepareFeatures:
    """Tests for feature preparation."""
    
    def test_converts_dict_to_numpy_array(self, prediction_service):
        """
        Test basic conversion of dict to numpy array.
        
        Acceptance Criteria: Maps feature dict to numpy array.
        """
        # Arrange: Feature dictionary
        features = {"feat1": 1.0, "feat2": 2.0, "feat3": 3.0}
        expected_features = ["feat1", "feat2", "feat3"]
        
        # Act: Prepare features
        result = prediction_service.prepare_features(features, expected_features)
        
        # Assert: Should be numpy array
        assert isinstance(result, np.ndarray)
        assert result.shape == (1, 3)
    
    def test_fills_missing_features_with_zero(self, prediction_service):
        """Test that missing features are filled with 0.0."""
        # Arrange: Missing feat3
        features = {"feat1": 1.0, "feat2": 2.0}
        expected_features = ["feat1", "feat2", "feat3"]
        
        # Act: Prepare features
        result = prediction_service.prepare_features(features, expected_features)
        
        # Assert: Missing feature should be 0.0
        expected_array = np.array([[1.0, 2.0, 0.0]], dtype=np.float32)
        np.testing.assert_array_almost_equal(result, expected_array)


if __name__ == "__main__":
    pytest.main([__file__, "-v"])