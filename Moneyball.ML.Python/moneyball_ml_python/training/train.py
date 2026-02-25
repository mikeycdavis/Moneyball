"""
Main training script for NBA betting models.

Acceptance Criteria:
- Executable via: python -m moneyball_ml_python.training.train
- Single execution trains all supported models
- Modular with prepare_features, train_model, clear orchestration
- Saves serialized models and metadata
- Outputs evaluation metrics for each model
"""

import json
import logging
import sys
import subprocess
from pathlib import Path
from datetime import datetime
from typing import Dict, Any, List

import joblib
import numpy as np
import pandas as pd
from sklearn.model_selection import train_test_split

# Import our modules
from moneyball_ml_python.training.model_config import get_all_model_configs, ModelConfig
from moneyball_ml_python.training.feature_engineering import (
    prepare_features,
    get_feature_columns
)
from moneyball_ml_python.training.model_trainer import (
    train_model,
    get_feature_importance
)
from moneyball_ml_python.data.data_loader import (
    load_training_data,
    validate_training_data
)

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
    handlers=[
        logging.StreamHandler(sys.stdout),
        logging.FileHandler('training.log')
    ]
)
logger = logging.getLogger(__name__)


def main():
    """
    Main training orchestration function.
    
    Acceptance Criteria:
    - Clear orchestration logic in main()
    - Trains all models in MODEL_CONFIGS
    - Saves models and metadata
    - Prints evaluation metrics
    """
    logger.info("=" * 80)
    logger.info("NBA BETTING MODELS - TRAINING PIPELINE")
    logger.info("=" * 80)
    
    # Get Git commit SHA for versioning
    git_commit = get_git_commit_sha()
    python_version = f"{sys.version_info.major}.{sys.version_info.minor}.{sys.version_info.micro}"
    
    logger.info(f"Git Commit: {git_commit}")
    logger.info(f"Python Version: {python_version}")
    logger.info(f"Training Start: {datetime.now().isoformat()}")
    
    # Step 1: Load training data
    logger.info("\n" + "=" * 80)
    logger.info("STEP 1: Loading Training Data")
    logger.info("=" * 80)
    
    # Acceptance Criteria: Supports SportsRadar API or CSV
    df = load_training_data(source="synthetic")  # Can be "csv" or "sportsradar"
    validate_training_data(df)
    
    logger.info(f"Loaded {len(df)} training samples")
    logger.info(f"Date range: {df['game_date'].min()} to {df['game_date'].max()}")
    
    # Step 2: Get all model configurations
    # Acceptance Criteria: Single execution trains all supported models
    model_configs = get_all_model_configs()
    
    logger.info("\n" + "=" * 80)
    logger.info(f"STEP 2: Training {len(model_configs)} Models")
    logger.info("=" * 80)
    
    for i, config in enumerate(model_configs, 1):
        logger.info(f"\nModel {i}/{len(model_configs)}: {config.name}")
        logger.info(f"  Description: {config.description}")
        logger.info(f"  Type: {config.model_type.value}")
    
    # Create models directory
    models_dir = Path("models")
    models_dir.mkdir(exist_ok=True)
    
    # Track all results
    training_results = []
    
    # Step 3: Train each model
    for i, config in enumerate(model_configs, 1):
        logger.info("\n" + "=" * 80)
        logger.info(f"Training Model {i}/{len(model_configs)}: {config.name}")
        logger.info("=" * 80)
        
        try:
            # Train the model
            # Acceptance Criteria: prepare_features(df, model_type), train_model(X, y, model_name)
            result = train_single_model(df, config, git_commit, python_version)
            
            # Save the model
            # Acceptance Criteria: Produces serialized model files and metadata
            save_model(result, models_dir)
            
            training_results.append(result)
            
            logger.info(f"✓ Successfully trained {config.name}")
            
        except Exception as e:
            logger.error(f"✗ Failed to train {config.name}: {e}", exc_info=True)
            training_results.append({
                'model_name': config.name,
                'status': 'failed',
                'error': str(e)
            })
    
    # Step 4: Print summary
    logger.info("\n" + "=" * 80)
    logger.info("TRAINING COMPLETE - SUMMARY")
    logger.info("=" * 80)
    
    successful = [r for r in training_results if r.get('status') != 'failed']
    failed = [r for r in training_results if r.get('status') == 'failed']
    
    logger.info(f"\nTotal Models: {len(training_results)}")
    logger.info(f"Successful: {len(successful)}")
    logger.info(f"Failed: {len(failed)}")
    
    if successful:
        logger.info("\nSuccessful Models:")
        for result in successful:
            metrics = result['metrics']
            logger.info(
                f"  {result['model_name']}: "
                f"Accuracy={metrics['accuracy']:.4f}, "
                f"AUC={metrics['auc']:.4f}"
            )
    
    if failed:
        logger.info("\nFailed Models:")
        for result in failed:
            logger.info(f"  {result['model_name']}: {result['error']}")
    
    logger.info(f"\nModels saved to: {models_dir.absolute()}")
    logger.info(f"Training End: {datetime.now().isoformat()}")
    logger.info("=" * 80)
    
    # Exit with error code if any models failed
    if failed:
        sys.exit(1)


def train_single_model(
    df: pd.DataFrame,
    config: ModelConfig,
    git_commit: str,
    python_version: str
) -> Dict[str, Any]:
    """
    Train a single model with all steps.
    
    Args:
        df: Training data
        config: Model configuration
        git_commit: Git commit SHA
        python_version: Python version string
        
    Returns:
        Dictionary with model, metadata, and metrics
    """
    logger.info(f"Preparing features for {config.name}...")
    
    # Step 1: Prepare features
    # Acceptance Criteria: prepare_features(df, model_type)
    feature_df = prepare_features(df, config.feature_groups)
    
    # Get feature columns and target
    feature_columns = get_feature_columns(feature_df, config.target)
    
    logger.info(f"Using {len(feature_columns)} features")
    logger.debug(f"Features: {feature_columns[:10]}...")  # Show first 10
    
    # Extract features and target
    X = feature_df[feature_columns].values
    y = feature_df[config.target].values
    
    logger.info(f"Target distribution: {np.bincount(y.astype(int))}")
    
    # Step 2: Train/validation split
    X_train, X_val, y_train, y_val = train_test_split(
        X, y, test_size=0.2, random_state=42, stratify=y
    )
    
    logger.info(
        f"Split: {len(X_train)} training, {len(X_val)} validation"
    )
    
    # Step 3: Train model
    # Acceptance Criteria: train_model(X, y, model_name)
    # Acceptance Criteria: Outputs probabilities (not just labels)
    model, metrics = train_model(
        X_train, y_train,
        X_val, y_val,
        config.model_type,
        config.hyperparameters or {}
    )
    
    # Step 4: Get feature importance
    feature_importance = get_feature_importance(model, feature_columns)
    
    # Step 5: Create metadata
    # Acceptance Criteria: Metadata includes all specified fields
    metadata = {
        'model_name': config.name,
        'description': config.description,
        'model_type': config.model_type.value,
        'target': config.target,
        'feature_groups': [fg.value for fg in config.feature_groups],
        'feature_list': feature_columns,
        'feature_importance': feature_importance,
        'training_timestamp': datetime.now().isoformat(),
        'git_commit_sha': git_commit,
        'python_version': python_version,
        'hyperparameters': config.hyperparameters,
        'metrics': metrics,
        'is_active': True,  # Mark as active for prediction service
        'expected_features': feature_columns  # For prepare_features in prediction
    }
    
    return {
        'model_name': config.name,
        'model': model,
        'metadata': metadata,
        'metrics': metrics,
        'feature_columns': feature_columns,
        'status': 'success'
    }


def save_model(result: Dict[str, Any], models_dir: Path) -> None:
    """
    Save model and metadata to disk.
    
    Acceptance Criteria:
    - Serialized model files (.pkl)
    - Metadata files (.json)
    - Each model saved separately
    
    Args:
        result: Training result dictionary
        models_dir: Directory to save models
    """
    model_name = result['model_name']
    
    # Save model as .pkl
    model_path = models_dir / f"{model_name}.pkl"
    logger.info(f"Saving model to: {model_path}")
    joblib.dump(result['model'], model_path, compress=3)
    
    # Save metadata as .json
    metadata_path = models_dir / f"{model_name}.json"
    logger.info(f"Saving metadata to: {metadata_path}")
    
    metadata = result['metadata'].copy()
    # Remove feature importance if too large
    if 'feature_importance' in metadata and len(metadata['feature_importance']) > 100:
        # Keep only top 20 features
        fi = metadata['feature_importance']
        sorted_fi = sorted(fi.items(), key=lambda x: x[1], reverse=True)[:20]
        metadata['feature_importance'] = dict(sorted_fi)
    
    with open(metadata_path, 'w') as f:
        json.dump(metadata, f, indent=2)
    
    logger.info(f"✓ Saved {model_name}")


def get_git_commit_sha() -> str:
    """
    Get current Git commit SHA.
    
    Acceptance Criteria: Metadata includes Git commit SHA
    
    Returns:
        Git commit SHA or 'unknown'
    """
    try:
        result = subprocess.run(
            ['git', 'rev-parse', 'HEAD'],
            capture_output=True,
            text=True,
            timeout=5
        )
        if result.returncode == 0:
            return result.stdout.strip()
    except Exception as e:
        logger.warning(f"Could not get git commit: {e}")
    
    return 'unknown'


if __name__ == "__main__":
    main()