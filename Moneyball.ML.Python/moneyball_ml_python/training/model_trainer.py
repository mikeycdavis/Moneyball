"""
Model trainer for NBA betting models.

Implements train_model(X, y, model_name) as per acceptance criteria.
Handles different model types and outputs probabilities.
"""

import logging
import time
from typing import Dict, Any, Tuple
import numpy as np
from sklearn.linear_model import LogisticRegression
from sklearn.ensemble import RandomForestClassifier
from sklearn.metrics import (
    accuracy_score, precision_score, recall_score, 
    f1_score, roc_auc_score, log_loss
)

try:
    import xgboost as xgb
    XGBOOST_AVAILABLE = True
except ImportError: # pragma: no cover
    XGBOOST_AVAILABLE = False
    
try:
    import lightgbm as lgb
    LIGHTGBM_AVAILABLE = True
except ImportError: # pragma: no cover
    LIGHTGBM_AVAILABLE = False

from moneyball_ml_python.training.model_config import ModelType

logger = logging.getLogger(__name__)


def train_model(
    X_train: np.ndarray,
    y_train: np.ndarray,
    X_val: np.ndarray,
    y_val: np.ndarray,
    model_type: ModelType,
    hyperparameters: Dict[str, Any]
) -> Tuple[Any, Dict[str, Any]]:
    """
    Train a single model and evaluate it.
    
    Acceptance Criteria: train_model(X, y, model_name)
    
    Args:
        X_train: Training features
        y_train: Training labels
        X_val: Validation features
        y_val: Validation labels
        model_type: Type of model to train
        hyperparameters: Model hyperparameters
        
    Returns:
        Tuple of (trained_model, metrics_dict)
        
    Acceptance Criteria - Model Evaluation Output:
    Prints for each model:
    - Accuracy
    - Precision
    - Recall
    - F1 Score
    - AUC
    - Log Loss
    - Number of training samples
    - Number of validation samples
    """
    logger.info(f"Training {model_type.value} model...")
    logger.info(f"Training samples: {len(X_train)}, Validation samples: {len(X_val)}")
    
    start_time = time.time()
    
    # Train model based on type
    if model_type == ModelType.LOGISTIC_REGRESSION:
        model = train_logistic_regression(X_train, y_train, hyperparameters)
    elif model_type == ModelType.RANDOM_FOREST:
        model = train_random_forest(X_train, y_train, hyperparameters)
    elif model_type == ModelType.XGBOOST:
        model = train_xgboost(X_train, y_train, hyperparameters)
    elif model_type == ModelType.LIGHTGBM:
        model = train_lightgbm(X_train, y_train, hyperparameters)
    else:
        raise ValueError(f"Unsupported model type: {model_type}")
    
    training_time = time.time() - start_time
    
    # Make predictions (Acceptance Criteria: outputs probabilities)
    y_pred = model.predict(X_val)
    y_pred_proba = model.predict_proba(X_val)[:, 1]  # Probability of positive class
    
    # Calculate metrics (Acceptance Criteria: Model Evaluation Output)
    metrics = {
        'accuracy': float(accuracy_score(y_val, y_pred)),
        'precision': float(precision_score(y_val, y_pred, zero_division=0)),
        'recall': float(recall_score(y_val, y_pred, zero_division=0)),
        'f1_score': float(f1_score(y_val, y_pred, zero_division=0)),
        'auc': float(roc_auc_score(y_val, y_pred_proba)),
        'log_loss': float(log_loss(y_val, y_pred_proba)),
        'training_samples': int(len(X_train)),
        'validation_samples': int(len(X_val)),
        'training_time_seconds': float(training_time)
    }
    
    # Log metrics (Acceptance Criteria: prints evaluation metrics)
    logger.info("=" * 60)
    logger.info(f"Model Evaluation Results:")
    logger.info(f"  Accuracy:             {metrics['accuracy']:.4f}")
    logger.info(f"  Precision:            {metrics['precision']:.4f}")
    logger.info(f"  Recall:               {metrics['recall']:.4f}")
    logger.info(f"  F1 Score:             {metrics['f1_score']:.4f}")
    logger.info(f"  AUC:                  {metrics['auc']:.4f}")
    logger.info(f"  Log Loss:             {metrics['log_loss']:.4f}")
    logger.info(f"  Training Samples:     {metrics['training_samples']}")
    logger.info(f"  Validation Samples:   {metrics['validation_samples']}")
    logger.info(f"  Training Time:        {metrics['training_time_seconds']:.2f}s")
    logger.info("=" * 60)
    
    return model, metrics


def train_logistic_regression(
    X_train: np.ndarray,
    y_train: np.ndarray,
    hyperparameters: Dict[str, Any]
) -> LogisticRegression:
    """
    Train Logistic Regression model.
    
    Args:
        X_train: Training features
        y_train: Training labels
        hyperparameters: Model hyperparameters
        
    Returns:
        Trained LogisticRegression model
    """
    logger.debug("Training Logistic Regression...")
    
    model = LogisticRegression(**hyperparameters)
    model.fit(X_train, y_train)
    
    return model


def train_random_forest(
    X_train: np.ndarray,
    y_train: np.ndarray,
    hyperparameters: Dict[str, Any]
) -> RandomForestClassifier:
    """
    Train Random Forest model.
    
    Args:
        X_train: Training features
        y_train: Training labels
        hyperparameters: Model hyperparameters
        
    Returns:
        Trained RandomForestClassifier model
    """
    logger.debug("Training Random Forest...")
    
    model = RandomForestClassifier(**hyperparameters)
    model.fit(X_train, y_train)
    
    return model


def train_xgboost(
    X_train: np.ndarray,
    y_train: np.ndarray,
    hyperparameters: Dict[str, Any]
) -> Any:
    """
    Train XGBoost model.
    
    Args:
        X_train: Training features
        y_train: Training labels
        hyperparameters: Model hyperparameters
        
    Returns:
        Trained XGBoost model
    """
    if not XGBOOST_AVAILABLE:
        raise ImportError(
            "XGBoost is not installed. Install with: pip install xgboost"
        )
    
    logger.debug("Training XGBoost...")
    
    # Default XGBoost parameters
    params = {
        'objective': 'binary:logistic',
        'eval_metric': 'logloss',
        'random_state': 42,
        **hyperparameters
    }
    
    dtrain = xgb.DMatrix(X_train, label=y_train)
    model = xgb.train(params, dtrain, num_boost_round=100)
    
    return model


def train_lightgbm(
    X_train: np.ndarray,
    y_train: np.ndarray,
    hyperparameters: Dict[str, Any]
) -> Any:
    """
    Train LightGBM model.
    
    Args:
        X_train: Training features
        y_train: Training labels
        hyperparameters: Model hyperparameters
        
    Returns:
        Trained LightGBM model
    """
    if not LIGHTGBM_AVAILABLE:
        raise ImportError(
            "LightGBM is not installed. Install with: pip install lightgbm"
        )
    
    logger.debug("Training LightGBM...")
    
    # Default LightGBM parameters
    params = {
        'objective': 'binary',
        'metric': 'binary_logloss',
        'random_state': 42,
        **hyperparameters
    }
    
    train_data = lgb.Dataset(X_train, label=y_train)
    model = lgb.train(params, train_data, num_boost_round=100)
    
    return model


def get_feature_importance(model: Any, feature_names: list) -> Dict[str, float]:
    """
    Get feature importance from trained model.
    
    Args:
        model: Trained model
        feature_names: List of feature names
        
    Returns:
        Dictionary mapping feature names to importance scores
    """
    importance_dict = {}
    
    try:
        if hasattr(model, 'feature_importances_'):
            # Scikit-learn models (Random Forest, etc.)
            importances = model.feature_importances_
            for name, importance in zip(feature_names, importances):
                importance_dict[name] = float(importance)
        elif hasattr(model, 'coef_'):
            # Linear models (Logistic Regression)
            coefficients = np.abs(model.coef_[0])
            for name, coef in zip(feature_names, coefficients):
                importance_dict[name] = float(coef)
        else:
            logger.warning("Model does not have feature importance attributes")
    except Exception as e: # pragma: no cover
        logger.error(f"Failed to extract feature importance: {e}")
    
    return importance_dict