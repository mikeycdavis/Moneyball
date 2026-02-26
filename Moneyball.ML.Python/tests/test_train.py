"""
Unit tests for train.py

Tests the main training orchestration script:
- main() - Main orchestration function
- train_single_model() - Training a single model
- save_model() - Saving model and metadata
- get_git_commit_sha() - Getting Git commit

Test Structure:
- Test orchestration logic
- Test model training workflow
- Test file saving
- Test acceptance criteria
- Test error handling
"""

import pytest
import json
import tempfile
import subprocess
from pathlib import Path
from unittest.mock import Mock, patch, MagicMock, call
import numpy as np
import pandas as pd
import joblib

# Import the module to test
from moneyball_ml_python.training.train import (
    main,
    train_single_model,
    save_model,
    get_git_commit_sha
)
from moneyball_ml_python.training.model_config import ModelConfig, ModelType, FeatureGroup


# ====================
# Fixtures
# ====================

@pytest.fixture
def sample_dataframe():
    """
    Create a sample DataFrame for testing.
    
    Returns:
        pd.DataFrame with training data
    """
    np.random.seed(42)
    n_samples = 100
    
    return pd.DataFrame({
        'game_id': range(n_samples),
        'game_date': pd.date_range('2024-01-01', periods=n_samples),
        'home_offensive_rating': np.random.uniform(100, 120, n_samples),
        'away_offensive_rating': np.random.uniform(100, 120, n_samples),
        'home_defensive_rating': np.random.uniform(100, 120, n_samples),
        'away_defensive_rating': np.random.uniform(100, 120, n_samples),
        'home_win': np.random.randint(0, 2, n_samples),
        'home_cover': np.random.randint(0, 2, n_samples),
        'total_over': np.random.randint(0, 2, n_samples)
    })


@pytest.fixture
def sample_model_config():
    """
    Create a sample ModelConfig for testing.
    
    Returns:
        ModelConfig instance
    """
    return ModelConfig(
        name="test_model",
        target="home_win",
        model_type=ModelType.LOGISTIC_REGRESSION,
        feature_groups=[FeatureGroup.TEAM_STATS],
        description="Test model",
        hyperparameters={'max_iter': 100, 'random_state': 42}
    )


@pytest.fixture
def temp_models_dir():
    """
    Create temporary models directory.
    
    Yields:
        Path to temporary directory
    """
    temp_dir = tempfile.mkdtemp()
    yield Path(temp_dir)
    
    # Cleanup
    import shutil
    shutil.rmtree(temp_dir, ignore_errors=True)


@pytest.fixture
def mock_training_result():
    """
    Create a mock training result.
    
    Returns:
        dict: Training result
    """
    mock_model = Mock()
    mock_model.predict_proba = Mock(return_value=np.array([[0.3, 0.7]]))
    
    return {
        'model_name': 'test_model',
        'model': mock_model,
        'metadata': {
            'model_name': 'test_model',
            'description': 'Test model',
            'model_type': 'logistic_regression',
            'target': 'home_win',
            'feature_groups': ['team_stats'],
            'feature_list': ['feat1', 'feat2', 'feat3'],
            'feature_importance': {'feat1': 0.5, 'feat2': 0.3, 'feat3': 0.2},
            'training_timestamp': '2024-01-01T00:00:00',
            'git_commit_sha': 'abc123',
            'python_version': '3.11.0',
            'hyperparameters': {'max_iter': 100},
            'metrics': {
                'accuracy': 0.85,
                'auc': 0.90,
                'precision': 0.88,
                'recall': 0.82
            },
            'is_active': True,
            'expected_features': ['feat1', 'feat2', 'feat3']
        },
        'metrics': {
            'accuracy': 0.85,
            'auc': 0.90
        },
        'feature_columns': ['feat1', 'feat2', 'feat3'],
        'status': 'success'
    }


# ====================
# Tests for main()
# ====================

class TestMain:
    """
    Tests for main orchestration function.
    
    Acceptance Criteria:
    - Executable via python -m moneyball_ml_python.training.train
    - Single execution trains all supported models
    - Clear orchestration logic in main()
    """
    
    @patch('moneyball_ml_python.training.train.load_training_data')
    @patch('moneyball_ml_python.training.train.get_all_model_configs')
    @patch('moneyball_ml_python.training.train.train_single_model')
    @patch('moneyball_ml_python.training.train.save_model')
    def test_orchestrates_complete_training_workflow(
        self,
        mock_save,
        mock_train_single,
        mock_get_configs,
        mock_load_data,
        sample_dataframe,
        sample_model_config,
        mock_training_result
    ):
        """
        Test that main() orchestrates complete workflow.
        
        Acceptance Criteria: Clear orchestration logic in main()
        """
        # Arrange: Mock all dependencies
        mock_load_data.return_value = sample_dataframe
        mock_get_configs.return_value = [sample_model_config]
        mock_train_single.return_value = mock_training_result
        
        # Act: Run main
        main()
        
        # Assert: All steps should be called
        mock_load_data.assert_called_once()
        mock_get_configs.assert_called_once()
        mock_train_single.assert_called_once()
        mock_save.assert_called_once()
    
    @patch('moneyball_ml_python.training.train.load_training_data')
    @patch('moneyball_ml_python.training.train.get_all_model_configs')
    @patch('moneyball_ml_python.training.train.train_single_model')
    @patch('moneyball_ml_python.training.train.save_model')
    def test_trains_all_models_in_single_execution(
        self,
        mock_save,
        mock_train_single,
        mock_get_configs,
        mock_load_data,
        sample_dataframe,
        mock_training_result
    ):
        """
        Test that all models are trained in single execution.
        
        Acceptance Criteria: Single execution trains all supported models
        """
        # Arrange: Multiple model configs
        config1 = ModelConfig(
            name="model1", target="home_win",
            model_type=ModelType.LOGISTIC_REGRESSION,
            feature_groups=[FeatureGroup.TEAM_STATS],
            description="Model 1", hyperparameters={}
        )
        config2 = ModelConfig(
            name="model2", target="home_cover",
            model_type=ModelType.RANDOM_FOREST,
            feature_groups=[FeatureGroup.TEAM_STATS],
            description="Model 2", hyperparameters={}
        )
        
        mock_load_data.return_value = sample_dataframe
        mock_get_configs.return_value = [config1, config2]
        mock_train_single.return_value = mock_training_result
        
        # Act: Run main
        main()
        
        # Assert: train_single_model should be called twice
        assert mock_train_single.call_count == 2
        assert mock_save.call_count == 2
    
    @patch('moneyball_ml_python.training.train.load_training_data')
    @patch('moneyball_ml_python.training.train.get_all_model_configs')
    @patch('moneyball_ml_python.training.train.train_single_model')
    def test_handles_training_failures_gracefully(
        self,
        mock_train_single,
        mock_get_configs,
        mock_load_data,
        sample_dataframe,
        sample_model_config
    ):
        """Test that training failures are handled gracefully."""
        # Arrange: Mock failure
        mock_load_data.return_value = sample_dataframe
        mock_get_configs.return_value = [sample_model_config]
        mock_train_single.side_effect = Exception("Training failed")
        
        # Act & Assert: Should not crash, but exit with code 1
        with pytest.raises(SystemExit) as exc_info:
            main()
        
        assert exc_info.value.code == 1
    
    @patch('moneyball_ml_python.training.train.load_training_data')
    @patch('moneyball_ml_python.training.train.get_all_model_configs')
    @patch('moneyball_ml_python.training.train.Path')
    def test_creates_models_directory(
        self,
        mock_path,
        mock_get_configs,
        mock_load_data,
        sample_dataframe
    ):
        """Test that models directory is created."""
        # Arrange
        mock_load_data.return_value = sample_dataframe
        mock_get_configs.return_value = []
        mock_models_dir = Mock()
        mock_path.return_value = mock_models_dir
        
        # Act
        main()
        
        # Assert: mkdir should be called
        mock_models_dir.mkdir.assert_called_once_with(exist_ok=True)


# ====================
# Tests for train_single_model()
# ====================

class TestTrainSingleModel:
    """
    Tests for single model training function.
    
    Acceptance Criteria:
    - Uses prepare_features(df, model_type)
    - Uses train_model(X, y, model_name)
    - Outputs probabilities
    """
    
    @patch('moneyball_ml_python.training.train.prepare_features')
    @patch('moneyball_ml_python.training.train.get_feature_columns')
    @patch('moneyball_ml_python.training.train.train_model')
    @patch('moneyball_ml_python.training.train.get_feature_importance')
    def test_returns_training_result(
        self,
        mock_importance,
        mock_train,
        mock_get_cols,
        mock_prepare,
        sample_dataframe,
        sample_model_config
    ):
        """Test that function returns complete training result."""
        # Arrange: Mock all steps
        mock_prepare.return_value = sample_dataframe
        mock_get_cols.return_value = ['feat1', 'feat2']
        
        mock_model = Mock()
        mock_metrics = {'accuracy': 0.85, 'auc': 0.90}
        mock_train.return_value = (mock_model, mock_metrics)
        mock_importance.return_value = {'feat1': 0.6, 'feat2': 0.4}
        
        # Act: Train model
        result = train_single_model(
            sample_dataframe,
            sample_model_config,
            'abc123',
            '3.11.0'
        )
        
        # Assert: Should return complete result
        assert 'model_name' in result
        assert 'model' in result
        assert 'metadata' in result
        assert 'metrics' in result
        assert 'feature_columns' in result
        assert 'status' in result
        assert result['status'] == 'success'
    
    @patch('moneyball_ml_python.training.train.prepare_features')
    @patch('moneyball_ml_python.training.train.get_feature_columns')
    @patch('moneyball_ml_python.training.train.train_model')
    @patch('moneyball_ml_python.training.train.get_feature_importance')
    def test_uses_prepare_features(
        self,
        mock_importance,
        mock_train,
        mock_get_cols,
        mock_prepare,
        sample_dataframe,
        sample_model_config
    ):
        """
        Test that prepare_features is called.
        
        Acceptance Criteria: Uses prepare_features(df, model_type)
        """
        # Arrange
        mock_prepare.return_value = sample_dataframe
        mock_get_cols.return_value = ['feat1', 'feat2']
        mock_train.return_value = (Mock(), {'accuracy': 0.85})
        mock_importance.return_value = {}
        
        # Act: Train model
        train_single_model(
            sample_dataframe,
            sample_model_config,
            'abc123',
            '3.11.0'
        )
        
        # Assert: prepare_features should be called with feature groups
        mock_prepare.assert_called_once_with(
            sample_dataframe,
            sample_model_config.feature_groups
        )
    
    @patch('moneyball_ml_python.training.train.prepare_features')
    @patch('moneyball_ml_python.training.train.get_feature_columns')
    @patch('moneyball_ml_python.training.train.train_model')
    @patch('moneyball_ml_python.training.train.get_feature_importance')
    def test_uses_train_model(
        self,
        mock_importance,
        mock_train,
        mock_get_cols,
        mock_prepare,
        sample_dataframe,
        sample_model_config
    ):
        """
        Test that train_model is called.
        
        Acceptance Criteria: Uses train_model(X, y, model_name)
        """
        # Arrange
        mock_prepare.return_value = sample_dataframe
        mock_get_cols.return_value = ['feat1', 'feat2']
        mock_train.return_value = (Mock(), {'accuracy': 0.85})
        mock_importance.return_value = {}
        
        # Act: Train model
        train_single_model(
            sample_dataframe,
            sample_model_config,
            'abc123',
            '3.11.0'
        )
        
        # Assert: train_model should be called
        assert mock_train.called
        call_args = mock_train.call_args
        assert call_args is not None
    
    @patch('moneyball_ml_python.training.train.prepare_features')
    @patch('moneyball_ml_python.training.train.get_feature_columns')
    @patch('moneyball_ml_python.training.train.train_model')
    @patch('moneyball_ml_python.training.train.get_feature_importance')
    def test_includes_git_commit_in_metadata(
        self,
        mock_importance,
        mock_train,
        mock_get_cols,
        mock_prepare,
        sample_dataframe,
        sample_model_config
    ):
        """
        Test that Git commit SHA is included in metadata.
        
        Acceptance Criteria: Metadata includes Git commit SHA
        """
        # Arrange
        mock_prepare.return_value = sample_dataframe
        mock_get_cols.return_value = ['feat1', 'feat2']
        mock_train.return_value = (Mock(), {'accuracy': 0.85})
        mock_importance.return_value = {}
        
        # Act: Train with specific git commit
        result = train_single_model(
            sample_dataframe,
            sample_model_config,
            'test_commit_sha_123',
            '3.11.0'
        )
        
        # Assert: Metadata should include git commit
        assert result['metadata']['git_commit_sha'] == 'test_commit_sha_123'
    
    @patch('moneyball_ml_python.training.train.prepare_features')
    @patch('moneyball_ml_python.training.train.get_feature_columns')
    @patch('moneyball_ml_python.training.train.train_model')
    @patch('moneyball_ml_python.training.train.get_feature_importance')
    def test_metadata_includes_all_required_fields(
        self,
        mock_importance,
        mock_train,
        mock_get_cols,
        mock_prepare,
        sample_dataframe,
        sample_model_config
    ):
        """
        Test that metadata includes all required fields.
        
        Acceptance Criteria: Metadata includes all specified fields
        """
        # Arrange
        mock_prepare.return_value = sample_dataframe
        mock_get_cols.return_value = ['feat1', 'feat2']
        mock_train.return_value = (Mock(), {'accuracy': 0.85})
        mock_importance.return_value = {'feat1': 0.5}
        
        # Act: Train model
        result = train_single_model(
            sample_dataframe,
            sample_model_config,
            'abc123',
            '3.11.0'
        )
        
        # Assert: Check all required metadata fields
        metadata = result['metadata']
        required_fields = [
            'model_name', 'description', 'model_type', 'target',
            'feature_groups', 'feature_list', 'feature_importance',
            'training_timestamp', 'git_commit_sha', 'python_version',
            'hyperparameters', 'metrics', 'is_active', 'expected_features'
        ]
        
        for field in required_fields:
            assert field in metadata, f"Missing metadata field: {field}"
    
    @patch('moneyball_ml_python.training.train.prepare_features')
    @patch('moneyball_ml_python.training.train.get_feature_columns')
    @patch('moneyball_ml_python.training.train.train_model')
    @patch('moneyball_ml_python.training.train.get_feature_importance')
    def test_marks_model_as_active(
        self,
        mock_importance,
        mock_train,
        mock_get_cols,
        mock_prepare,
        sample_dataframe,
        sample_model_config
    ):
        """Test that model is marked as active for prediction service."""
        # Arrange
        mock_prepare.return_value = sample_dataframe
        mock_get_cols.return_value = ['feat1', 'feat2']
        mock_train.return_value = (Mock(), {'accuracy': 0.85})
        mock_importance.return_value = {}
        
        # Act: Train model
        result = train_single_model(
            sample_dataframe,
            sample_model_config,
            'abc123',
            '3.11.0'
        )
        
        # Assert: Should be marked as active
        assert result['metadata']['is_active'] is True


# ====================
# Tests for save_model()
# ====================

class TestSaveModel:
    """
    Tests for model saving function.
    
    Acceptance Criteria:
    - Produces serialized model files (.pkl)
    - Produces metadata files (.json)
    - Each model saved separately
    """
    
    def test_saves_model_as_pkl(self, mock_training_result, temp_models_dir):
        """
        Test that model is saved as .pkl file.
        
        Acceptance Criteria: Serialized model files (.pkl)
        """
        # Act: Save model
        save_model(mock_training_result, temp_models_dir)
        
        # Assert: .pkl file should exist
        model_path = temp_models_dir / "test_model.pkl"
        assert model_path.exists(), "Model .pkl file should be created"
    
    def test_saves_metadata_as_json(self, mock_training_result, temp_models_dir):
        """
        Test that metadata is saved as .json file.
        
        Acceptance Criteria: Metadata files (.json)
        """
        # Act: Save model
        save_model(mock_training_result, temp_models_dir)
        
        # Assert: .json file should exist
        metadata_path = temp_models_dir / "test_model.json"
        assert metadata_path.exists(), "Metadata .json file should be created"
    
    def test_model_can_be_loaded(self, mock_training_result, temp_models_dir):
        """Test that saved model can be loaded."""
        # Act: Save and load
        save_model(mock_training_result, temp_models_dir)
        model_path = temp_models_dir / "test_model.pkl"
        loaded_model = joblib.load(model_path)
        
        # Assert: Should load successfully
        assert loaded_model is not None
    
    def test_metadata_is_valid_json(self, mock_training_result, temp_models_dir):
        """Test that saved metadata is valid JSON."""
        # Act: Save and load
        save_model(mock_training_result, temp_models_dir)
        metadata_path = temp_models_dir / "test_model.json"
        
        with open(metadata_path, 'r') as f:
            metadata = json.load(f)
        
        # Assert: Should parse as JSON
        assert isinstance(metadata, dict)
        assert 'model_name' in metadata
    
    def test_saves_each_model_separately(self, temp_models_dir):
        """
        Test that each model is saved separately.
        
        Acceptance Criteria: Each model saved separately
        """
        # Arrange: Two different models
        result1 = {
            'model_name': 'model1',
            'model': Mock(),
            'metadata': {'model_name': 'model1', 'feature_importance': {}}
        }
        result2 = {
            'model_name': 'model2',
            'model': Mock(),
            'metadata': {'model_name': 'model2', 'feature_importance': {}}
        }
        
        # Act: Save both models
        save_model(result1, temp_models_dir)
        save_model(result2, temp_models_dir)
        
        # Assert: Both should be saved separately
        assert (temp_models_dir / "model1.pkl").exists()
        assert (temp_models_dir / "model1.json").exists()
        assert (temp_models_dir / "model2.pkl").exists()
        assert (temp_models_dir / "model2.json").exists()
    
    def test_truncates_large_feature_importance(self, temp_models_dir):
        """Test that large feature importance is truncated."""
        # Arrange: Result with many features
        result = {
            'model_name': 'test_model',
            'model': Mock(),
            'metadata': {
                'model_name': 'test_model',
                'feature_importance': {f'feat{i}': i/200 for i in range(200)}
            }
        }
        
        # Act: Save model
        save_model(result, temp_models_dir)
        
        # Assert: Metadata should have truncated importance
        metadata_path = temp_models_dir / "test_model.json"
        with open(metadata_path, 'r') as f:
            metadata = json.load(f)
        
        # Should keep only top 20
        assert len(metadata['feature_importance']) <= 20


# ====================
# Tests for get_git_commit_sha()
# ====================

class TestGetGitCommitSha:
    """
    Tests for Git commit SHA retrieval.
    
    Acceptance Criteria: Metadata includes Git commit SHA
    """
    
    @patch('subprocess.run')
    def test_returns_commit_sha_when_available(self, mock_run):
        """Test that function returns commit SHA when git is available."""
        # Arrange: Mock successful git command
        mock_result = Mock()
        mock_result.returncode = 0
        mock_result.stdout = "abc123def456\n"
        mock_run.return_value = mock_result
        
        # Act: Get commit SHA
        sha = get_git_commit_sha()
        
        # Assert: Should return SHA without newline
        assert sha == "abc123def456"
    
    @patch('subprocess.run')
    def test_returns_unknown_when_git_fails(self, mock_run):
        """Test that function returns 'unknown' when git fails."""
        # Arrange: Mock failed git command
        mock_result = Mock()
        mock_result.returncode = 1
        mock_run.return_value = mock_result
        
        # Act: Get commit SHA
        sha = get_git_commit_sha()
        
        # Assert: Should return 'unknown'
        assert sha == "unknown"
    
    @patch('subprocess.run')
    def test_returns_unknown_on_exception(self, mock_run):
        """Test that function returns 'unknown' on exception."""
        # Arrange: Mock exception
        mock_run.side_effect = Exception("Git not found")
        
        # Act: Get commit SHA
        sha = get_git_commit_sha()
        
        # Assert: Should return 'unknown'
        assert sha == "unknown"
    
    @patch('subprocess.run')
    def test_calls_git_rev_parse(self, mock_run):
        """Test that function calls git rev-parse HEAD."""
        # Arrange
        mock_result = Mock()
        mock_result.returncode = 0
        mock_result.stdout = "abc123\n"
        mock_run.return_value = mock_result
        
        # Act: Get commit SHA
        get_git_commit_sha()
        
        # Assert: Should call git rev-parse HEAD
        mock_run.assert_called_once()
        call_args = mock_run.call_args[0][0]
        assert call_args == ['git', 'rev-parse', 'HEAD']


# ====================
# Integration Tests
# ====================

class TestTrainIntegration:
    """Integration tests for complete training workflows."""
    
    @patch('moneyball_ml_python.training.train.prepare_features')
    @patch('moneyball_ml_python.training.train.get_feature_columns')
    @patch('moneyball_ml_python.training.train.train_model')
    @patch('moneyball_ml_python.training.train.get_feature_importance')
    def test_train_and_save_workflow(
        self,
        mock_importance,
        mock_train,
        mock_get_cols,
        mock_prepare,
        sample_dataframe,
        sample_model_config,
        temp_models_dir
    ):
        """Test complete train and save workflow."""
        # Arrange
        mock_prepare.return_value = sample_dataframe
        mock_get_cols.return_value = ['feat1', 'feat2']
        mock_model = Mock()
        mock_train.return_value = (mock_model, {'accuracy': 0.85})
        mock_importance.return_value = {'feat1': 0.5}
        
        # Act: Train and save
        result = train_single_model(
            sample_dataframe,
            sample_model_config,
            'abc123',
            '3.11.0'
        )
        save_model(result, temp_models_dir)
        
        # Assert: Files should exist and be loadable
        model_path = temp_models_dir / "test_model.pkl"
        metadata_path = temp_models_dir / "test_model.json"
        
        assert model_path.exists()
        assert metadata_path.exists()
        
        # Load and verify
        loaded_model = joblib.load(model_path)
        assert loaded_model is not None
        
        with open(metadata_path, 'r') as f:
            metadata = json.load(f)
        assert metadata['model_name'] == 'test_model'


if __name__ == "__main__":
    pytest.main([__file__, "-v"])