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

class FakeFullModel:
    def predict_proba(self, X):
        return np.array([[0.35, 0.65]])

    def predict(self, X):
        return np.array([0.65])

class FakeModelMissingPredictProba:
    def predict(self, X):
        return np.array([0.65])

class FakeFullModelWithException:
    def predict_proba(self, X):
        raise Exception("Prediction failed")

    def predict(self, X):
        raise Exception("Prediction failed")


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
    model = FakeFullModel()
    
    if (not hasattr(model, "predict_proba")):
        raise Exception("predict_proba method missing from mock model 1")

    return FakeFullModel()


@pytest.fixture
def mock_trained_model_missing_predict_proba():
    """
    Create a mock trained model for testing.
    
    The mock model:
    - No predict_proba method
    - Returns realistic probabilities
    - Simulates a real scikit-learn classifier
    
    Returns:
        Mock model object
    """    
    model = FakeModelMissingPredictProba()

    # Delete predict_proba method to simulate missing method
    if (hasattr(model, "predict_proba")):
        raise Exception("Failed to remove predict_proba method from mock model 3")

    return model


@pytest.fixture
def mock_trained_exception_model():
    """
    Create a mock trained exception model for testing.
    
    The mock model:
    - Has predict_proba method
    - Throws exceptions
    
    Returns:
        Mock model exception object
    """    
    return FakeFullModelWithException()


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
def sample_model_missing_predict_proba_file(temp_models_dir, mock_trained_model_missing_predict_proba):
    """
    Create a sample model missing predict_proba file (.pkl) for testing.
    
    Args:
        temp_models_dir: Temporary directory fixture
        mock_trained_model_missing_predict_proba: Mock model fixture missing predict_proba
        
    Returns:
        tuple: (model_path, metadata_path)
    """
    # Save mock model as .pkl file
    model_path = temp_models_dir / "test_model_v3.pkl"
    joblib.dump(mock_trained_model_missing_predict_proba, model_path)
    
    # Create accompanying metadata file
    metadata = {
        "is_active": True,
        "expected_features": ["feature1", "feature2", "feature3"],
        "description": "Test model v3"
    }
    metadata_path = temp_models_dir / "test_model_v3.json"
    with open(metadata_path, 'w') as f:
        json.dump(metadata, f)
    
    return model_path, metadata_path


@pytest.fixture
def sample_model_exception_file(temp_models_dir, mock_trained_exception_model):
    """
    Create a sample model exception file (.pkl) for testing.
    
    Args:
        temp_models_dir: Temporary directory fixture
        mock_trained_exception_model: Mock model exception fixture
        
    Returns:
        tuple: (model_path, metadata_path)
    """
    # Save mock model as .pkl file
    model_path = temp_models_dir / "test_model_v2.pkl"
    joblib.dump(mock_trained_exception_model, model_path)
    
    # Create accompanying metadata file
    metadata = {
        "is_active": True,
        "expected_features": ["feature1", "feature2", "feature3"],
        "description": "Test model v2"
    }
    metadata_path = temp_models_dir / "test_model_v2json"
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


@pytest.fixture
def service_with_models(prediction_service):
    """
    Create a PredictionService with pre-loaded models for testing.
    
    This fixture simulates a service that has already loaded several models,
    allowing us to test the get methods without actually loading model files.
    
    Args:
        prediction_service: Base prediction service fixture
        
    Returns:
        PredictionService: Service with 3 mock models loaded
    """
    # Simulate loaded models by directly populating the internal dictionaries
    # This avoids the need to create actual model files
    
    # Model 1: home_win
    mock_model_1 = Mock()
    prediction_service.loaded_models['home_win'] = {
        'model': mock_model_1,
        'path': 'models/home_win.pkl',
        'loaded_at': 1234567890.123
    }
    prediction_service.model_metadata['home_win'] = {
        'is_active': True,
        'expected_features': ['feat1', 'feat2', 'feat3'],
        'description': 'Predicts home team win probability'
    }
    
    # Model 2: home_cover
    mock_model_2 = Mock()
    prediction_service.loaded_models['home_cover'] = {
        'model': mock_model_2,
        'path': 'models/home_cover.pkl',
        'loaded_at': 1234567891.456
    }
    prediction_service.model_metadata['home_cover'] = {
        'is_active': True,
        'expected_features': ['feat1', 'feat2', 'feat3', 'feat4'],
        'description': 'Predicts home team covers spread'
    }
    
    # Model 3: total_over
    mock_model_3 = Mock()
    prediction_service.loaded_models['total_over'] = {
        'model': mock_model_3,
        'path': 'models/total_over.pkl',
        'loaded_at': 1234567892.789
    }
    prediction_service.model_metadata['total_over'] = {
        'is_active': True,
        'expected_features': ['feat1', 'feat2'],
        'description': 'Predicts total goes over line'
    }
    
    return prediction_service


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
    
    def test_prediction_fails(self, prediction_service, sample_model_exception_file):
        """
        Test that prediction fails.
        
        Acceptance Criteria: Exception thrown
        """
        # Arrange: Load model
        prediction_service.load_models()
        features = {"feature1": 0.5, "feature2": 0.7, "feature3": 0.3}
        
        # Act: Make prediction
        result = prediction_service.predict("test_model_v2", features)
        
        # Assert: Should throw exception
        assert result == {'error': 'Prediction failed', 'model_version': 'test_model_v2'}
    
    def test_prediction_missing_predict_proba(self, prediction_service, sample_model_missing_predict_proba_file):
        """
        Test that prediction fails.
        
        Acceptance Criteria: Exception thrown
        """
        # Arrange: Load model
        prediction_service.load_models()
        features = {"feature1": 0.5, "feature2": 0.7, "feature3": 0.3}
        
        # Act: Make prediction
        result = prediction_service.predict("test_model_v3", features)
        
        # Assert: Should throw exception
        assert 0 <= result["home_win_probability"] <= 1
        assert 0 <= result["away_win_probability"] <= 1


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
    
    def test_no_expected_features(self, prediction_service):
        """Test that missing expected_features are skipped."""
        # Arrange: Missing expected_features
        features = {"feat1": 1.0, "feat2": 2.0}
        
        # Act: Prepare features
        result = prediction_service.prepare_features(features, [])
        
        # Assert: Should be numpy array
        assert isinstance(result, np.ndarray)
        assert result.shape == (1, 2)
    
    def test_invalid_data(self, prediction_service):
        """Test that bad data zeroes out values."""
        # Arrange: Feature dictionary
        features = {"feat1": "test 1", "feat2": "test 2", "feat3": "test 3"}
        expected_features = ["feat1", "feat2", "feat3"]
        
        # Act: Prepare features
        result = prediction_service.prepare_features(features, expected_features)
        
        # Assert: Should be numpy array
        assert isinstance(result, np.ndarray)
        assert result.shape == (1, 3)
        assert result[0, 0] == 0.0
        assert result[0, 1] == 0.0
        assert result[0, 2] == 0.0
    
    def test_no_data(self, prediction_service):
        """Test that missing features and empty_features returns empty array."""
        # Arrange: Missing features and expected_features
        
        # Act: Prepare features
        result = prediction_service.prepare_features(None, None)
        
        # Assert: Should be empty numpy array
        assert isinstance(result, np.ndarray)
        assert result.shape == (1, 0)


# ====================
# Tests for get_loaded_models()
# ====================

class TestGetLoadedModels:
    """Tests for get_loaded_models() method."""
    
    def test_returns_list(self, prediction_service):
        """
        Test that get_loaded_models() returns a list.
        
        Even with no models loaded, should return an empty list.
        """
        # Act: Get loaded models
        result = prediction_service.get_loaded_models()
        
        # Assert: Should be a list
        assert isinstance(result, list), "Should return a list"
    
    def test_returns_empty_list_when_no_models_loaded(self, prediction_service):
        """
        Test that empty list is returned when no models are loaded.
        
        This is the initial state before load_models() is called.
        """
        # Act: Get loaded models from empty service
        result = prediction_service.get_loaded_models()
        
        # Assert: Should be empty
        assert len(result) == 0, "Should return empty list when no models loaded"
        assert result == [], "Should be an empty list"
    
    def test_returns_all_loaded_model_names(self, service_with_models):
        """
        Test that all loaded model names are returned.
        
        Should return the keys from loaded_models dictionary.
        """
        # Act: Get loaded models
        result = service_with_models.get_loaded_models()
        
        # Assert: Should contain all 3 model names
        assert len(result) == 3, "Should return 3 model names"
        assert "home_win" in result, "Should include home_win"
        assert "home_cover" in result, "Should include home_cover"
        assert "total_over" in result, "Should include total_over"
    
    def test_returns_model_names_as_strings(self, service_with_models):
        """
        Test that all returned items are strings.
        
        Model names should be string identifiers.
        """
        # Act: Get loaded models
        result = service_with_models.get_loaded_models()
        
        # Assert: All items should be strings
        for model_name in result:
            assert isinstance(model_name, str), f"Model name {model_name} should be string"
    
    def test_does_not_modify_internal_state(self, service_with_models):
        """
        Test that calling get_loaded_models() doesn't modify the service.
        
        Should be a read-only operation.
        """
        # Arrange: Get initial state
        initial_count = len(service_with_models.loaded_models)
        
        # Act: Get loaded models multiple times
        result1 = service_with_models.get_loaded_models()
        result2 = service_with_models.get_loaded_models()
        
        # Assert: Internal state unchanged
        assert len(service_with_models.loaded_models) == initial_count
        assert result1 == result2, "Should return same results each time"
    
    def test_returns_keys_only_not_values(self, service_with_models):
        """
        Test that only model names (keys) are returned, not the full model objects.
        
        Should return list of strings, not dictionary objects.
        """
        # Act: Get loaded models
        result = service_with_models.get_loaded_models()
        
        # Assert: Should be model names only
        assert isinstance(result, list)
        assert all(isinstance(name, str) for name in result)
        assert all(not isinstance(name, dict) for name in result), "Should not return dicts"


# ====================
# Tests for get_model_info()
# ====================

class TestGetModelInfo:
    """Tests for get_model_info() method."""
    
    def test_returns_none_for_nonexistent_model(self, prediction_service):
        """
        Test that None is returned when model doesn't exist.
        
        This allows the caller to check if a model is loaded.
        """
        # Act: Try to get info for non-existent model
        result = prediction_service.get_model_info("nonexistent_model")
        
        # Assert: Should return None
        assert result is None, "Should return None for non-existent model"
    
    def test_returns_none_for_nonexistent_model_with_models_loaded(self, service_with_models):
        """
        Test that None is returned for non-existent model even when other models exist.
        
        Should only return info for the specific requested model.
        """
        # Act: Try to get info for non-existent model
        result = service_with_models.get_model_info("nonexistent_model")
        
        # Assert: Should return None
        assert result is None, "Should return None even with other models loaded"
    
    def test_returns_dict_for_existing_model(self, service_with_models):
        """
        Test that a dictionary is returned for an existing model.
        
        The info dictionary contains model metadata and load information.
        """
        # Act: Get info for existing model
        result = service_with_models.get_model_info("home_win")
        
        # Assert: Should return a dictionary
        assert isinstance(result, dict), "Should return dictionary for existing model"
        assert result is not None
    
    def test_includes_path_in_info(self, service_with_models):
        """
        Test that model path is included in the info.
        
        Path shows where the model was loaded from.
        """
        # Act: Get model info
        info = service_with_models.get_model_info("home_win")
        
        # Assert: Should include path
        assert "path" in info, "Info should include 'path' field"
        assert info["path"] == "models/home_win.pkl"
        assert isinstance(info["path"], str)
    
    def test_includes_loaded_at_timestamp(self, service_with_models):
        """
        Test that loaded_at timestamp is included in the info.
        
        Timestamp indicates when the model was loaded into memory.
        """
        # Act: Get model info
        info = service_with_models.get_model_info("home_win")
        
        # Assert: Should include loaded_at
        assert "loaded_at" in info, "Info should include 'loaded_at' field"
        assert isinstance(info["loaded_at"], float), "Timestamp should be float"
        assert info["loaded_at"] == 1234567890.123
    
    def test_includes_metadata(self, service_with_models):
        """
        Test that model metadata is included in the info.
        
        Metadata contains model configuration and expected features.
        """
        # Act: Get model info
        info = service_with_models.get_model_info("home_win")
        
        # Assert: Should include metadata
        assert "metadata" in info, "Info should include 'metadata' field"
        assert isinstance(info["metadata"], dict), "Metadata should be dictionary"
    
    def test_metadata_contains_expected_fields(self, service_with_models):
        """
        Test that metadata contains expected fields.
        
        Metadata should include: is_active, expected_features, description
        """
        # Act: Get model info
        info = service_with_models.get_model_info("home_win")
        metadata = info["metadata"]
        
        # Assert: Check metadata fields
        assert "is_active" in metadata, "Metadata should include 'is_active'"
        assert "expected_features" in metadata, "Metadata should include 'expected_features'"
        assert "description" in metadata, "Metadata should include 'description'"
        
        # Check values
        assert metadata["is_active"] == True
        assert metadata["expected_features"] == ['feat1', 'feat2', 'feat3']
        assert metadata["description"] == 'Predicts home team win probability'
    
    def test_does_not_include_model_object(self, service_with_models):
        """
        Test that the actual model object is NOT included in info.
        
        Model objects are large and not serializable, so they should be excluded.
        This is important for API responses.
        """
        # Act: Get model info
        info = service_with_models.get_model_info("home_win")
        
        # Assert: Should NOT include model object
        assert "model" not in info, "Info should NOT include 'model' object"
    
    def test_does_not_modify_original_loaded_models(self, service_with_models):
        """
        Test that getting info doesn't modify the original loaded_models dict.
        
        The method should return a copy, not the original.
        """
        # Arrange: Get original model dict
        original_model_dict = service_with_models.loaded_models['home_win']
        
        # Act: Get model info
        info = service_with_models.get_model_info("home_win")
        
        # Assert: Original should still have 'model' key
        assert 'model' in original_model_dict, "Original dict should still have 'model'"
        assert 'model' not in info, "Returned info should not have 'model'"
    
    def test_returns_correct_info_for_different_models(self, service_with_models):
        """
        Test that each model returns its own specific information.
        
        Different models should have different paths, timestamps, and metadata.
        """
        # Act: Get info for different models
        info_home_win = service_with_models.get_model_info("home_win")
        info_home_cover = service_with_models.get_model_info("home_cover")
        info_total_over = service_with_models.get_model_info("total_over")
        
        # Assert: Each should have different information
        assert info_home_win["path"] != info_home_cover["path"]
        assert info_home_cover["path"] != info_total_over["path"]
        
        assert info_home_win["loaded_at"] != info_home_cover["loaded_at"]
        assert info_home_cover["loaded_at"] != info_total_over["loaded_at"]
        
        assert len(info_home_win["metadata"]["expected_features"]) == 3
        assert len(info_home_cover["metadata"]["expected_features"]) == 4
        assert len(info_total_over["metadata"]["expected_features"]) == 2
    
    def test_handles_model_without_metadata(self, prediction_service):
        """
        Test that method handles models that don't have metadata.
        
        Should return empty dict for metadata if not found.
        """
        # Arrange: Add model without metadata
        mock_model = Mock()
        prediction_service.loaded_models['test_model'] = {
            'model': mock_model,
            'path': 'models/test.pkl',
            'loaded_at': 1234567890.0
        }
        # Note: No metadata added to model_metadata dict
        
        # Act: Get model info
        info = prediction_service.get_model_info('test_model')
        
        # Assert: Should have empty metadata dict
        assert info is not None
        assert "metadata" in info
        assert info["metadata"] == {}, "Should return empty dict for missing metadata"
    
    def test_case_sensitive_model_lookup(self, service_with_models):
        """
        Test that model name lookup is case-sensitive.
        
        "home_win" and "HOME_WIN" should be treated as different models.
        """
        # Act: Try to get model with wrong case
        result = service_with_models.get_model_info("HOME_WIN")  # Wrong case
        
        # Assert: Should not find it
        assert result is None, "Model lookup should be case-sensitive"
        
        # Correct case should work
        result_correct = service_with_models.get_model_info("home_win")
        assert result_correct is not None, "Correct case should find model"


# ====================
# Integration Tests
# ====================

class TestGetMethodsIntegration:
    """Integration tests for get_loaded_models and get_model_info together."""
    
    def test_get_loaded_models_then_get_info_for_each(self, service_with_models):
        """
        Test typical usage pattern: get all models, then get info for each.
        
        This simulates how an API endpoint might list models and then
        provide detailed info for each one.
        """
        # Act: Get all model names
        model_names = service_with_models.get_loaded_models()
        
        # Act: Get info for each model
        model_infos = {}
        for model_name in model_names:
            info = service_with_models.get_model_info(model_name)
            model_infos[model_name] = info
        
        # Assert: Should have info for all models
        assert len(model_infos) == 3, "Should have info for all 3 models"
        assert all(info is not None for info in model_infos.values())
        assert all("path" in info for info in model_infos.values())
        assert all("metadata" in info for info in model_infos.values())
    
    def test_get_info_for_all_returned_models(self, service_with_models):
        """
        Test that get_model_info works for all models returned by get_loaded_models.
        
        Every model name returned by get_loaded_models should be valid for get_model_info.
        """
        # Act: Get all models and their info
        model_names = service_with_models.get_loaded_models()
        
        # Assert: get_model_info should work for each
        for model_name in model_names:
            info = service_with_models.get_model_info(model_name)
            assert info is not None, f"get_model_info should work for {model_name}"
            assert isinstance(info, dict), f"Info should be dict for {model_name}"
    
    def test_consistency_between_methods(self, service_with_models):
        """
        Test consistency between get_loaded_models and get_model_info.
        
        The number of models returned by get_loaded_models should match
        the number of models that return non-None info.
        """
        # Act: Get model names
        model_names = service_with_models.get_loaded_models()
        
        # Count how many models return valid info
        valid_info_count = 0
        for model_name in model_names:
            info = service_with_models.get_model_info(model_name)
            if info is not None:
                valid_info_count += 1
        
        # Assert: Should match
        assert len(model_names) == valid_info_count, \
            "Every loaded model should return valid info"


if __name__ == "__main__":
    pytest.main([__file__, "-v"])