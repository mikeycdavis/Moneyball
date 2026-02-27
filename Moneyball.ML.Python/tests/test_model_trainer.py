"""
Unit tests for model_trainer.py

Tests all model training functions:
- train_model() - Main training function
- train_logistic_regression() - Logistic regression training
- train_random_forest() - Random forest training
- train_xgboost() - XGBoost training (if available)
- train_lightgbm() - LightGBM training (if available)
- get_feature_importance() - Feature importance extraction

Test Structure:
- Test each model type training
- Test metrics calculation
- Test acceptance criteria
- Test error handling
- Test feature importance extraction
"""

import enum
import pytest
import numpy as np
from unittest.mock import Mock, patch, MagicMock
from sklearn.linear_model import LogisticRegression
from sklearn.ensemble import RandomForestClassifier

# Import the module to test
from moneyball_ml_python.training.model_trainer import (
    train_model,
    train_logistic_regression,
    train_random_forest,
    train_xgboost,
    train_lightgbm,
    get_feature_importance,
    XGBOOST_AVAILABLE,
    LIGHTGBM_AVAILABLE
)
from moneyball_ml_python.training.model_config import ModelType


# ====================
# Fixtures
# ====================

@pytest.fixture
def sample_training_data():
    """
    Create sample training data for testing.
    
    Returns:
        tuple: (X_train, y_train, X_val, y_val)
    """
    np.random.seed(42)
    
    # Create training data
    X_train = np.random.randn(100, 5)  # 100 samples, 5 features
    y_train = np.random.randint(0, 2, 100)  # Binary labels
    
    # Create validation data
    X_val = np.random.randn(20, 5)  # 20 samples, 5 features
    y_val = np.random.randint(0, 2, 20)  # Binary labels
    
    return X_train, y_train, X_val, y_val


@pytest.fixture
def sample_hyperparameters():
    """
    Create sample hyperparameters.
    
    Returns:
        dict: Hyperparameters for testing
    """
    return {
        'random_state': 42,
        'max_iter': 1000
    }


# ====================
# Tests for train_model()
# ====================

class TestTrainModel:
    """
    Tests for main train_model() function.
    
    Acceptance Criteria: train_model(X, y, model_name)
    """
    
    def test_returns_model_and_metrics(self, sample_training_data, sample_hyperparameters):
        """
        Test that train_model returns tuple of (model, metrics).
        
        Acceptance Criteria: Returns trained model and metrics.
        """
        # Arrange: Get training data
        X_train, y_train, X_val, y_val = sample_training_data
        
        # Act: Train model
        model, metrics = train_model(
            X_train, y_train, X_val, y_val,
            ModelType.LOGISTIC_REGRESSION,
            sample_hyperparameters
        )
        
        # Assert: Should return model and metrics
        assert model is not None
        assert isinstance(metrics, dict)
    
    def test_trains_logistic_regression(self, sample_training_data, sample_hyperparameters):
        """Test training logistic regression model."""
        # Arrange
        X_train, y_train, X_val, y_val = sample_training_data
        
        # Act: Train logistic regression
        model, metrics = train_model(
            X_train, y_train, X_val, y_val,
            ModelType.LOGISTIC_REGRESSION,
            sample_hyperparameters
        )
        
        # Assert: Should be LogisticRegression
        assert isinstance(model, LogisticRegression)
    
    def test_trains_random_forest(self, sample_training_data):
        """Test training random forest model."""
        # Arrange
        X_train, y_train, X_val, y_val = sample_training_data
        hyperparameters = {'n_estimators': 10, 'random_state': 42}
        
        # Act: Train random forest
        model, metrics = train_model(
            X_train, y_train, X_val, y_val,
            ModelType.RANDOM_FOREST,
            hyperparameters
        )
        
        # Assert: Should be RandomForestClassifier
        assert isinstance(model, RandomForestClassifier)
    
    def test_raises_error_for_unsupported_model_type(self, sample_training_data):
        """Test that unsupported model type raises ValueError."""
        # Arrange
        X_train, y_train, X_val, y_val = sample_training_data
        fake_enum = {"name": "Unsupported", "value": "Unsupported"}
        fake_model_type = enum.Enum('ModelType', fake_enum)
        
        # Act & Assert: Should raise ValueError
        with pytest.raises(ValueError, match="Unsupported model type"):
            train_model(
                X_train, y_train, X_val, y_val,
                fake_model_type,
                {}
            )
    
    def test_outputs_probabilities(self, sample_training_data, sample_hyperparameters):
        """
        Test that model outputs probabilities.
        
        Acceptance Criteria: Model outputs probabilities (not just labels).
        """
        # Arrange
        X_train, y_train, X_val, y_val = sample_training_data
        
        # Act: Train model
        model, metrics = train_model(
            X_train, y_train, X_val, y_val,
            ModelType.LOGISTIC_REGRESSION,
            sample_hyperparameters
        )
        
        # Assert: Model should have predict_proba
        assert hasattr(model, 'predict_proba')
        
        # Test predict_proba works
        probabilities = model.predict_proba(X_val)
        assert probabilities.shape[0] == len(X_val)
        assert probabilities.shape[1] == 2  # Binary classification
    
    def test_calculates_all_metrics(self, sample_training_data, sample_hyperparameters):
        """
        Test that all required metrics are calculated.
        
        Acceptance Criteria - Model Evaluation Output:
        - Accuracy
        - Precision
        - Recall
        - F1 Score
        - AUC
        - Log Loss
        - Number of training samples
        - Number of validation samples
        """
        # Arrange
        X_train, y_train, X_val, y_val = sample_training_data
        
        # Act: Train model
        model, metrics = train_model(
            X_train, y_train, X_val, y_val,
            ModelType.LOGISTIC_REGRESSION,
            sample_hyperparameters
        )
        
        # Assert: All required metrics should be present
        required_metrics = [
            'accuracy', 'precision', 'recall', 'f1_score',
            'auc', 'log_loss',
            'training_samples', 'validation_samples',
            'training_time_seconds'
        ]
        
        for metric in required_metrics:
            assert metric in metrics, f"Missing metric: {metric}"
    
    def test_metrics_have_correct_types(self, sample_training_data, sample_hyperparameters):
        """Test that metrics have correct data types."""
        # Arrange
        X_train, y_train, X_val, y_val = sample_training_data
        
        # Act: Train model
        model, metrics = train_model(
            X_train, y_train, X_val, y_val,
            ModelType.LOGISTIC_REGRESSION,
            sample_hyperparameters
        )
        
        # Assert: Check types
        assert isinstance(metrics['accuracy'], float)
        assert isinstance(metrics['precision'], float)
        assert isinstance(metrics['recall'], float)
        assert isinstance(metrics['f1_score'], float)
        assert isinstance(metrics['auc'], float)
        assert isinstance(metrics['log_loss'], float)
        assert isinstance(metrics['training_samples'], int)
        assert isinstance(metrics['validation_samples'], int)
        assert isinstance(metrics['training_time_seconds'], float)
    
    def test_metrics_in_valid_ranges(self, sample_training_data, sample_hyperparameters):
        """Test that metrics are in valid ranges."""
        # Arrange
        X_train, y_train, X_val, y_val = sample_training_data
        
        # Act: Train model
        model, metrics = train_model(
            X_train, y_train, X_val, y_val,
            ModelType.LOGISTIC_REGRESSION,
            sample_hyperparameters
        )
        
        # Assert: Check ranges
        assert 0 <= metrics['accuracy'] <= 1
        assert 0 <= metrics['precision'] <= 1
        assert 0 <= metrics['recall'] <= 1
        assert 0 <= metrics['f1_score'] <= 1
        assert 0 <= metrics['auc'] <= 1
        assert metrics['log_loss'] >= 0
        assert metrics['training_samples'] == 100
        assert metrics['validation_samples'] == 20
        assert metrics['training_time_seconds'] > 0
    
    def test_records_sample_counts_correctly(self, sample_training_data, sample_hyperparameters):
        """Test that sample counts are recorded correctly."""
        # Arrange
        X_train, y_train, X_val, y_val = sample_training_data
        
        # Act: Train model
        model, metrics = train_model(
            X_train, y_train, X_val, y_val,
            ModelType.LOGISTIC_REGRESSION,
            sample_hyperparameters
        )
        
        # Assert: Sample counts should match
        assert metrics['training_samples'] == len(X_train)
        assert metrics['validation_samples'] == len(X_val)


# ====================
# Tests for train_logistic_regression()
# ====================

class TestTrainLogisticRegression:
    """Tests for logistic regression training."""
    
    def test_returns_logistic_regression_model(self, sample_training_data):
        """Test that function returns LogisticRegression model."""
        # Arrange
        X_train, y_train, _, _ = sample_training_data
        hyperparameters = {'max_iter': 1000, 'random_state': 42}
        
        # Act: Train model
        model = train_logistic_regression(X_train, y_train, hyperparameters)
        
        # Assert: Should be LogisticRegression
        assert isinstance(model, LogisticRegression)
    
    def test_model_is_fitted(self, sample_training_data):
        """Test that model is fitted after training."""
        # Arrange
        X_train, y_train, _, _ = sample_training_data
        hyperparameters = {'max_iter': 1000, 'random_state': 42}
        
        # Act: Train model
        model = train_logistic_regression(X_train, y_train, hyperparameters)
        
        # Assert: Model should be fitted
        assert hasattr(model, 'coef_')
        assert hasattr(model, 'intercept_')
    
    def test_can_make_predictions(self, sample_training_data):
        """Test that trained model can make predictions."""
        # Arrange
        X_train, y_train, X_val, _ = sample_training_data
        hyperparameters = {'max_iter': 1000, 'random_state': 42}
        
        # Act: Train and predict
        model = train_logistic_regression(X_train, y_train, hyperparameters)
        predictions = model.predict(X_val)
        
        # Assert: Should return predictions
        assert len(predictions) == len(X_val)
        assert all(p in [0, 1] for p in predictions)
    
    def test_uses_hyperparameters(self, sample_training_data):
        """Test that hyperparameters are applied."""
        # Arrange
        X_train, y_train, _, _ = sample_training_data
        hyperparameters = {'max_iter': 500, 'random_state': 123}
        
        # Act: Train model
        model = train_logistic_regression(X_train, y_train, hyperparameters)
        
        # Assert: Hyperparameters should be applied
        assert model.max_iter == 500
        assert model.random_state == 123


# ====================
# Tests for train_random_forest()
# ====================

class TestTrainRandomForest:
    """Tests for random forest training."""
    
    def test_returns_random_forest_model(self, sample_training_data):
        """Test that function returns RandomForestClassifier model."""
        # Arrange
        X_train, y_train, _, _ = sample_training_data
        hyperparameters = {'n_estimators': 10, 'random_state': 42}
        
        # Act: Train model
        model = train_random_forest(X_train, y_train, hyperparameters)
        
        # Assert: Should be RandomForestClassifier
        assert isinstance(model, RandomForestClassifier)
    
    def test_model_is_fitted(self, sample_training_data):
        """Test that model is fitted after training."""
        # Arrange
        X_train, y_train, _, _ = sample_training_data
        hyperparameters = {'n_estimators': 10, 'random_state': 42}
        
        # Act: Train model
        model = train_random_forest(X_train, y_train, hyperparameters)
        
        # Assert: Model should be fitted
        assert hasattr(model, 'estimators_')
        assert len(model.estimators_) > 0
    
    def test_can_make_predictions(self, sample_training_data):
        """Test that trained model can make predictions."""
        # Arrange
        X_train, y_train, X_val, _ = sample_training_data
        hyperparameters = {'n_estimators': 10, 'random_state': 42}
        
        # Act: Train and predict
        model = train_random_forest(X_train, y_train, hyperparameters)
        predictions = model.predict(X_val)
        
        # Assert: Should return predictions
        assert len(predictions) == len(X_val)
        assert all(p in [0, 1] for p in predictions)
    
    def test_has_feature_importances(self, sample_training_data):
        """Test that random forest has feature importances."""
        # Arrange
        X_train, y_train, _, _ = sample_training_data
        hyperparameters = {'n_estimators': 10, 'random_state': 42}
        
        # Act: Train model
        model = train_random_forest(X_train, y_train, hyperparameters)
        
        # Assert: Should have feature importances
        assert hasattr(model, 'feature_importances_')
        assert len(model.feature_importances_) == X_train.shape[1]


# ====================
# Tests for train_xgboost()
# ====================

class TestTrainXGBoost:
    """Tests for XGBoost training."""
    
    @pytest.mark.skipif(not XGBOOST_AVAILABLE, reason="XGBoost not installed")
    def test_returns_xgboost_model(self, sample_training_data):
        """Test that function returns XGBoost model."""
        # Arrange
        X_train, y_train, _, _ = sample_training_data
        hyperparameters = {}
        
        # Act: Train model
        model = train_xgboost(X_train, y_train, hyperparameters)
        
        # Assert: Should return a model
        assert model is not None
    
    def test_raises_error_when_xgboost_not_available(self, sample_training_data):
        """Test that ImportError is raised when XGBoost not available."""
        # Arrange
        X_train, y_train, _, _ = sample_training_data
        
        # Mock XGBOOST_AVAILABLE to False
        with patch('moneyball_ml_python.training.model_trainer.XGBOOST_AVAILABLE', False):
            # Act & Assert: Should raise ImportError
            with pytest.raises(ImportError, match="XGBoost is not installed"):
                train_xgboost(X_train, y_train, {})


# ====================
# Tests for train_lightgbm()
# ====================

class TestTrainLightGBM:
    """Tests for LightGBM training."""
    
    @pytest.mark.skipif(not LIGHTGBM_AVAILABLE, reason="LightGBM not installed")
    def test_returns_lightgbm_model(self, sample_training_data):
        """Test that function returns LightGBM model."""
        # Arrange
        X_train, y_train, _, _ = sample_training_data
        hyperparameters = {}
        
        # Act: Train model
        model = train_lightgbm(X_train, y_train, hyperparameters)
        
        # Assert: Should return a model
        assert model is not None
    
    def test_raises_error_when_lightgbm_not_available(self, sample_training_data):
        """Test that ImportError is raised when LightGBM not available."""
        # Arrange
        X_train, y_train, _, _ = sample_training_data
        
        # Mock LIGHTGBM_AVAILABLE to False
        with patch('moneyball_ml_python.training.model_trainer.LIGHTGBM_AVAILABLE', False):
            # Act & Assert: Should raise ImportError
            with pytest.raises(ImportError, match="LightGBM is not installed"):
                train_lightgbm(X_train, y_train, {})


# ====================
# Tests for get_feature_importance()
# ====================

class TestGetFeatureImportance:
    """Tests for feature importance extraction."""
    
    def test_extracts_from_random_forest(self, sample_training_data):
        """Test feature importance extraction from Random Forest."""
        # Arrange: Train random forest
        X_train, y_train, _, _ = sample_training_data
        model = train_random_forest(X_train, y_train, {'n_estimators': 10, 'random_state': 42})
        feature_names = ['feat1', 'feat2', 'feat3', 'feat4', 'feat5']
        
        # Act: Get feature importance
        importance = get_feature_importance(model, feature_names)
        
        # Assert: Should return dictionary
        assert isinstance(importance, dict)
        assert len(importance) == len(feature_names)
        
        # All features should be present
        for name in feature_names:
            assert name in importance
            assert isinstance(importance[name], float)
    
    def test_extracts_from_logistic_regression(self, sample_training_data):
        """Test feature importance extraction from Logistic Regression."""
        # Arrange: Train logistic regression
        X_train, y_train, _, _ = sample_training_data
        model = train_logistic_regression(X_train, y_train, {'max_iter': 1000, 'random_state': 42})
        feature_names = ['feat1', 'feat2', 'feat3', 'feat4', 'feat5']
        
        # Act: Get feature importance
        importance = get_feature_importance(model, feature_names)
        
        # Assert: Should return dictionary with coefficients
        assert isinstance(importance, dict)
        assert len(importance) == len(feature_names)
        
        # All should be non-negative (using abs)
        for name in feature_names:
            assert name in importance
            assert importance[name] >= 0
    
    def test_handles_model_without_importance(self):
        """Test handling of models without feature importance."""
        # Arrange: Mock model without importance attributes
        model = Mock()
        del model.feature_importances_
        del model.coef_
        feature_names = ['feat1', 'feat2']
        
        # Act: Get feature importance
        importance = get_feature_importance(model, feature_names)
        
        # Assert: Should return empty dict
        assert isinstance(importance, dict)
        assert len(importance) == 0
    
    def test_returns_float_values(self, sample_training_data):
        """Test that importance values are floats."""
        # Arrange
        X_train, y_train, _, _ = sample_training_data
        model = train_random_forest(X_train, y_train, {'n_estimators': 10, 'random_state': 42})
        feature_names = ['feat1', 'feat2', 'feat3', 'feat4', 'feat5']
        
        # Act: Get feature importance
        importance = get_feature_importance(model, feature_names)
        
        # Assert: All values should be floats
        for value in importance.values():
            assert isinstance(value, float)


# ====================
# Integration Tests
# ====================

class TestModelTrainerIntegration:
    """Integration tests for complete training workflows."""
    
    def test_complete_training_workflow(self, sample_training_data):
        """
        Test complete training workflow.
        
        This simulates actual usage in train.py.
        """
        # Arrange
        X_train, y_train, X_val, y_val = sample_training_data
        hyperparameters = {'max_iter': 1000, 'random_state': 42}
        
        # Act: Train model
        model, metrics = train_model(
            X_train, y_train, X_val, y_val,
            ModelType.LOGISTIC_REGRESSION,
            hyperparameters
        )
        
        # Get feature importance
        feature_names = [f'feat{i}' for i in range(X_train.shape[1])]
        importance = get_feature_importance(model, feature_names)
        
        # Assert: Complete workflow should work
        assert model is not None
        assert len(metrics) > 0
        assert len(importance) > 0
        
        # Model should be usable for predictions
        predictions = model.predict(X_val)
        probabilities = model.predict_proba(X_val)
        
        assert len(predictions) == len(X_val)
        assert probabilities.shape[0] == len(X_val)
    
    def test_training_multiple_model_types(self, sample_training_data):
        """Test training different model types sequentially."""
        # Arrange
        X_train, y_train, X_val, y_val = sample_training_data
        
        model_types = [
            (ModelType.LOGISTIC_REGRESSION, {'max_iter': 1000, 'random_state': 42}),
            (ModelType.RANDOM_FOREST, {'n_estimators': 10, 'random_state': 42})
        ]
        
        # Act & Assert: Train each model type
        for model_type, hyperparams in model_types:
            model, metrics = train_model(
                X_train, y_train, X_val, y_val,
                model_type,
                hyperparams
            )
            
            assert model is not None
            assert 'accuracy' in metrics
            assert 'auc' in metrics


# ====================
# Performance Tests
# ====================

class TestModelTrainerPerformance:
    """Tests for performance characteristics."""
    
    def test_training_completes_quickly(self, sample_training_data):
        """Test that training completes in reasonable time."""
        # Arrange
        X_train, y_train, X_val, y_val = sample_training_data
        hyperparameters = {'max_iter': 1000, 'random_state': 42}
        
        # Act: Train model
        model, metrics = train_model(
            X_train, y_train, X_val, y_val,
            ModelType.LOGISTIC_REGRESSION,
            hyperparameters
        )
        
        # Assert: Should complete quickly (< 5 seconds for small dataset)
        assert metrics['training_time_seconds'] < 5.0


if __name__ == "__main__":
    pytest.main([__file__, "-v"])