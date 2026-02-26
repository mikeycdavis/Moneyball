"""
Unit tests for Flask application (app.py)

Tests all Flask endpoints:
- POST /predict/<version> - Make predictions
- GET /models - List all loaded models
- GET /models/<version> - Get model info
- GET /health - Health check

Test Structure:
- Test successful responses (200 OK)
- Test error responses (404, 400, 500)
- Test request/response formats
- Test acceptance criteria
- Test service integration
"""

import pytest
import json
from unittest.mock import Mock, patch

# Import Flask app
from moneyball_ml_python.app import app
from moneyball_ml_python.prediction.predict import PredictionService


# ====================
# Fixtures
# ====================

@pytest.fixture
def client():
    """
    Create a Flask test client.
    
    The test client allows us to make requests to the Flask app
    without running a server.
    
    Yields:
        FlaskClient: Test client for making requests
    """
    app.config['TESTING'] = True
    with app.test_client() as client:
        yield client


@pytest.fixture
def mock_prediction_service():
    """
    Create a mock PredictionService for testing.
    
    This mock service:
    - Returns predictable responses
    - Doesn't require actual model files
    - Allows testing Flask routes in isolation
    
    Returns:
        Mock: Mocked PredictionService
    """
    service = Mock(spec=PredictionService)
    
    # Mock get_loaded_models to return sample models
    service.get_loaded_models.return_value = ["model_v1", "model_v2", "home_win"]
    
    # Mock predict to return sample prediction
    service.predict.return_value = {
        "home_win_probability": 0.68,
        "away_win_probability": 0.32,
        "confidence": 0.85,
        "model_version": "model_v1",
        "prediction_time_ms": 45.3
    }
    
    # Mock get_model_info to return sample info
    service.get_model_info.return_value = {
        "path": "models/model_v1.pkl",
        "loaded_at": 1234567890.123,
        "metadata": {
            "is_active": True,
            "expected_features": ["feat1", "feat2"],
            "description": "Test model"
        }
    }
    
    return service


@pytest.fixture
def app_with_service(mock_prediction_service):
    """
    Configure app with mocked prediction service.
    
    This fixture sets up the global prediction_service variable
    that the Flask routes use.
    
    Args:
        mock_prediction_service: Mocked service fixture
    """
    # Import the app module to set global variable
    import moneyball_ml_python.app as app_module
    
    # Store original service
    original_service = app_module.prediction_service
    
    # Set mock service
    app_module.prediction_service = mock_prediction_service
    
    yield
    
    # Restore original service after test
    app_module.prediction_service = original_service


# ====================
# Tests for POST /predict/<version>
# ====================

class TestPredictEndpoint:
    """
    Tests for the prediction endpoint.
    
    Acceptance Criteria:
    - Returns probabilities in < 200ms
    - Returns 404 JSON if model not found
    """
    
    def test_predict_success(self, client, app_with_service, mock_prediction_service):
        """
        Test successful prediction request.
        
        Acceptance Criteria: Returns probabilities in < 200ms.
        """
        # Arrange: Prepare request data
        request_data = {
            "features": {
                "home_offensive_rating": 115.2,
                "away_offensive_rating": 112.1,
                "home_defensive_rating": 110.5,
                "away_defensive_rating": 113.2
            }
        }
        
        # Act: Make POST request
        response = client.post(
            '/predict/model_v1',
            data=json.dumps(request_data),
            content_type='application/json'
        )
        
        # Assert: Check response
        assert response.status_code == 200, "Should return 200 OK"
        
        data = response.get_json()
        assert "home_win_probability" in data
        assert "away_win_probability" in data
        assert "confidence" in data
        assert "model_version" in data
        assert "prediction_time_ms" in data
        
        # Verify service was called correctly
        mock_prediction_service.predict.assert_called_once_with(
            "model_v1",
            request_data["features"]
        )
    
    def test_predict_returns_probabilities(self, client, app_with_service):
        """
        Test that prediction returns probability values.
        
        Acceptance Criteria: Returns probabilities.
        """
        # Arrange: Request data
        request_data = {
            "features": {"feat1": 0.5, "feat2": 0.7}
        }
        
        # Act: Make request
        response = client.post(
            '/predict/model_v1',
            data=json.dumps(request_data),
            content_type='application/json'
        )
        
        # Assert: Check probability values
        data = response.get_json()
        assert 0 <= data["home_win_probability"] <= 1
        assert 0 <= data["away_win_probability"] <= 1
        assert 0 <= data["confidence"] <= 1
    
    def test_predict_model_not_found(self, client, app_with_service, mock_prediction_service):
        """
        Test 404 response when model doesn't exist.
        
        Acceptance Criteria: Returns 404 JSON if model not found.
        """
        # Arrange: Mock service to return None (model not found)
        mock_prediction_service.predict.return_value = None
        
        request_data = {
            "features": {"feat1": 0.5}
        }
        
        # Act: Make request with non-existent model
        response = client.post(
            '/predict/nonexistent_model',
            data=json.dumps(request_data),
            content_type='application/json'
        )
        
        # Assert: Should return 404
        assert response.status_code == 404, "Should return 404 Not Found"
        
        data = response.get_json()
        assert "error" in data
        assert "nonexistent_model" in data["error"]
        assert "available_models" in data
    
    def test_predict_missing_features(self, client, app_with_service):
        """
        Test 400 response when features are missing.
        """
        # Arrange: Request without features
        request_data = {}
        
        # Act: Make request
        response = client.post(
            '/predict/model_v1',
            data=json.dumps(request_data),
            content_type='application/json'
        )
        
        # Assert: Should return 400 Bad Request
        assert response.status_code == 400
        
        data = response.get_json()
        assert "error" in data
        assert "features" in data["error"].lower()
    
    def test_predict_empty_features(self, client, app_with_service):
        """
        Test 400 response when features dict is empty.
        """
        # Arrange: Request with empty features
        request_data = {
            "features": {}
        }
        
        # Act: Make request
        response = client.post(
            '/predict/model_v1',
            data=json.dumps(request_data),
            content_type='application/json'
        )
        
        # Assert: Should return 400
        assert response.status_code == 400
    
    def test_predict_no_json_body(self, client, app_with_service):
        """
        Test 400 response when request has no JSON body.
        """
        # Act: Make request without JSON
        response = client.post('/predict/model_v1')
        
        # Assert: Should return 400
        assert response.status_code == 400
    
    def test_predict_service_not_initialized(self, client):
        """
        Test 500 response when prediction service is not initialized.
        """
        # Arrange: Set service to None
        import moneyball_ml_python.app as app_module
        original_service = app_module.prediction_service
        app_module.prediction_service = None
        
        try:
            # Act: Make request
            request_data = {"features": {"feat1": 0.5}}
            response = client.post(
                '/predict/model_v1',
                data=json.dumps(request_data),
                content_type='application/json'
            )
            
            # Assert: Should return 500
            assert response.status_code == 500
            data = response.get_json()
            assert "error" in data
        finally:
            # Cleanup: Restore service
            app_module.prediction_service = original_service


# ====================
# Tests for GET /models
# ====================

class TestListModelsEndpoint:
    """Tests for listing all models."""
    
    def test_list_models_success(self, client, app_with_service, mock_prediction_service):
        """
        Test successful listing of all models.
        """
        # Act: Make GET request
        response = client.get('/models')
        
        # Assert: Check response
        assert response.status_code == 200
        
        data = response.get_json()
        assert "models" in data
        assert "count" in data
        assert isinstance(data["models"], list)
        assert data["count"] == len(data["models"])
        
        # Verify service was called
        mock_prediction_service.get_loaded_models.assert_called_once()
    
    def test_list_models_returns_correct_models(self, client, app_with_service, mock_prediction_service):
        """
        Test that correct model list is returned.
        """
        # Act: Make request
        response = client.get('/models')
        
        # Assert: Check models match mock
        data = response.get_json()
        assert data["models"] == ["model_v1", "model_v2", "home_win"]
        assert data["count"] == 3
    
    def test_list_models_empty(self, client, app_with_service, mock_prediction_service):
        """
        Test listing when no models are loaded.
        """
        # Arrange: Mock to return empty list
        mock_prediction_service.get_loaded_models.return_value = []
        
        # Act: Make request
        response = client.get('/models')
        
        # Assert: Should still return 200 with empty list
        assert response.status_code == 200
        data = response.get_json()
        assert data["models"] == []
        assert data["count"] == 0


# ====================
# Tests for GET /models/<version>
# ====================

class TestGetModelEndpoint:
    """Tests for getting model information."""
    
    def test_get_model_success(self, client, app_with_service, mock_prediction_service):
        """
        Test successful retrieval of model info.
        """
        # Act: Make GET request
        response = client.get('/models/model_v1')
        
        # Assert: Check response
        assert response.status_code == 200
        
        data = response.get_json()
        assert "path" in data
        assert "loaded_at" in data
        assert "metadata" in data
        
        # Verify service was called
        mock_prediction_service.get_model_info.assert_called_once_with("model_v1")
    
    def test_get_model_not_found(self, client, app_with_service, mock_prediction_service):
        """
        Test 404 response when model doesn't exist.
        """
        # Arrange: Mock to return None
        mock_prediction_service.get_model_info.return_value = None
        
        # Act: Make request
        response = client.get('/models/nonexistent_model')
        
        # Assert: Should return 404
        assert response.status_code == 404
        
        data = response.get_json()
        assert "error" in data
        assert "nonexistent_model" in data["error"]
        assert "available_models" in data


# ====================
# Tests for GET /health
# ====================

class TestHealthEndpoint:
    """Tests for health check endpoint."""
    
    def test_health_check_success(self, client, app_with_service):
        """
        Test successful health check.
        """
        # Act: Make GET request
        response = client.get('/health')
        
        # Assert: Check response
        assert response.status_code == 200
        
        data = response.get_json()
        assert data["status"] == "healthy"
        assert data["service"] == "moneyball-ml-python"
        assert "models_loaded" in data
    
    def test_health_check_models_count(self, client, app_with_service, mock_prediction_service):
        """
        Test that health check includes models count.
        """
        # Act: Make request
        response = client.get('/health')
        
        # Assert: Check models count
        data = response.get_json()
        assert data["models_loaded"] == 3  # From mock
    
    def test_health_check_service_not_initialized(self, client):
        """
        Test health check when service is not initialized.
        
        Should still return 200 but with 0 models.
        """
        # Arrange: Set service to None
        import moneyball_ml_python.app as app_module
        original_service = app_module.prediction_service
        app_module.prediction_service = None
        
        try:
            # Act: Make request
            response = client.get('/health')
            
            # Assert: Should return 200 with 0 models
            assert response.status_code == 200
            data = response.get_json()
            assert data["models_loaded"] == 0
        finally:
            # Cleanup
            app_module.prediction_service = original_service


# ====================
# Integration Tests
# ====================

class TestFlaskAppIntegration:
    """Integration tests for complete workflows."""
    
    def test_complete_prediction_workflow(self, client, app_with_service):
        """
        Test complete workflow: health → list models → get model → predict.
        
        This simulates a typical API usage pattern.
        """
        # Step 1: Health check
        response = client.get('/health')
        assert response.status_code == 200
        
        # Step 2: List models
        response = client.get('/models')
        assert response.status_code == 200
        models = response.get_json()["models"]
        assert len(models) > 0
        
        # Step 3: Get first model info
        model_name = models[0]
        response = client.get(f'/models/{model_name}')
        assert response.status_code == 200
        
        # Step 4: Make prediction
        request_data = {"features": {"feat1": 0.5, "feat2": 0.7}}
        response = client.post(
            f'/predict/{model_name}',
            data=json.dumps(request_data),
            content_type='application/json'
        )
        assert response.status_code == 200


# ====================
# HTTP Method Tests
# ====================

class TestHTTPMethods:
    """Test that endpoints only accept correct HTTP methods."""
    
    def test_predict_only_accepts_post(self, client, app_with_service):
        """Test that /predict only accepts POST."""
        # GET should not be allowed
        response = client.get('/predict/model_v1')
        assert response.status_code == 405  # Method Not Allowed
    
    def test_models_only_accepts_get(self, client, app_with_service):
        """Test that /models only accepts GET."""
        # POST should not be allowed
        response = client.post('/models')
        assert response.status_code == 405
    
    def test_health_only_accepts_get(self, client, app_with_service):
        """Test that /health only accepts GET."""
        # POST should not be allowed
        response = client.post('/health')
        assert response.status_code == 405


# ====================
# Response Format Tests
# ====================

class TestResponseFormats:
    """Test that responses have correct JSON format."""
    
    def test_all_responses_are_json(self, client, app_with_service):
        """Test that all endpoints return JSON."""
        endpoints = [
            ('GET', '/health'),
            ('GET', '/models'),
            ('GET', '/models/model_v1')
        ]
        
        for method, path in endpoints:
            response = client.get(path)
            assert response.content_type == 'application/json'
            assert response.get_json() is not None


if __name__ == "__main__":
    pytest.main([__file__, "-v"])