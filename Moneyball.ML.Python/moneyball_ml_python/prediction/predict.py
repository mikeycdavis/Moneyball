"""
Prediction service for loading and serving ML models.

Acceptance Criteria:
- All IsActive Python models loaded at startup
- POST /predict returns probabilities in < 200ms
- Model not found returns 404 JSON
- prepare_features maps feature dict to numpy array
"""

import json
import logging
import time
from pathlib import Path
from typing import Dict, List, Optional, Any

import joblib
import numpy as np

logger = logging.getLogger(__name__)


class PredictionService:
    """Service for loading and serving ML model predictions."""

    def __init__(self, models_dir: str = "models"):
        """
        Initialize the prediction service.
        
        Args:
            models_dir: Directory containing model files (.pkl, .joblib)
        """
        self.models_dir = Path(models_dir)
        self.loaded_models: Dict[str, Dict[str, Any]] = {}
        self.model_metadata: Dict[str, Dict[str, Any]] = {}
        
        logger.info(f"Initialized PredictionService with models_dir: {self.models_dir}")

    def load_models(self) -> None:
        """
        Load all active models at startup.
        
        Acceptance Criteria: All IsActive Python models loaded at startup.
        
        Process:
        1. Scans models directory for .pkl and .joblib files
        2. Loads metadata from accompanying .json files
        3. Only loads models where is_active=True
        4. Stores models in memory for fast prediction
        """
        logger.info("Starting model loading...")
        start_time = time.time()
        
        # Find all model files
        model_files = list(self.models_dir.glob("*.pkl")) + list(self.models_dir.glob("*.joblib"))
        
        if not model_files:
            logger.warning(f"No model files found in {self.models_dir}")
            return
        
        loaded_count = 0
        skipped_count = 0
        
        # Load each model
        for model_path in model_files:
            try:
                # Extract version from filename (e.g., "NBA_LogReg_v1.pkl" -> "NBA_LogReg_v1")
                model_version = model_path.stem
                
                # Load metadata from .json file
                metadata_path = model_path.with_suffix('.json')
                metadata = self._load_metadata(metadata_path)
                
                # Check if model is active (Acceptance Criteria)
                if not metadata.get('is_active', True):
                    logger.info(f"Skipping inactive model: {model_version}")
                    skipped_count += 1
                    continue
                
                # Load the model
                logger.info(f"Loading model: {model_version}")
                model = joblib.load(model_path)
                
                # Store in memory
                self.loaded_models[model_version] = {
                    'model': model,
                    'path': str(model_path),
                    'loaded_at': time.time()
                }
                
                self.model_metadata[model_version] = metadata
                loaded_count += 1
                
                logger.info(f"✓ Loaded: {model_version}")
                
            except Exception as e: # pragma: no cover
                logger.error(f"Failed to load {model_path}: {e}")
                continue
        
        elapsed = time.time() - start_time
        logger.info(
            f"Model loading complete: {loaded_count} loaded, "
            f"{skipped_count} skipped, took {elapsed:.2f}s"
        )
        
        if loaded_count > 0:
            logger.info(f"Available models: {list(self.loaded_models.keys())}")

    def _load_metadata(self, metadata_path: Path) -> Dict[str, Any]:
        """
        Load model metadata from JSON file.
        
        Expected format:
        {
            "is_active": true,
            "expected_features": ["HomeWinRate", "AwayWinRate", ...],
            "description": "NBA Model v1"
        }
        """
        if not metadata_path.exists():
            return {'is_active': True, 'expected_features': []}
        
        try:
            with open(metadata_path, 'r') as f:
                return json.load(f)
        except Exception as e: # pragma: no cover
            logger.warning(f"Failed to load metadata from {metadata_path}: {e}")
            return {'is_active': True, 'expected_features': []}

    def predict(self, version: str, features: Dict[str, Any]) -> Optional[Dict[str, Any]]:
        """
        Make a prediction using the specified model.
        
        Acceptance Criteria:
        - Returns probabilities in < 200ms
        - Returns None if model not found (caller returns 404)
        - Uses prepare_features to map dict to numpy array
        
        Args:
            version: Model version (e.g., "first_team_to_20", "home_cover")
            features: Dict of feature names to values
            
        Returns:
            Dict with probabilities, or None if model not found
        """
        # Start timing (Acceptance Criteria: < 200ms)
        start_time = time.time()
        
        # Check if model exists (Acceptance Criteria: 404 if not found)
        if version not in self.loaded_models:
            logger.warning(f"Model not found: {version}")
            return None  # Caller returns 404
        
        try:
            # Get model
            model_info = self.loaded_models[version]
            model = model_info['model']
            
            # Get expected features
            metadata = self.model_metadata.get(version, {})
            expected_features = metadata.get('expected_features', [])
            
            # Prepare features (Acceptance Criteria: map dict to numpy array)
            feature_array = self.prepare_features(features, expected_features)
            
            # Make prediction
            if hasattr(model, 'predict_proba'):
                # Get probabilities
                probabilities = model.predict_proba(feature_array)
                
                # Binary classification: [[prob_class_0, prob_class_1]]
                home_win_prob = float(probabilities[0][1])
                away_win_prob = float(probabilities[0][0])
                
                # Confidence = distance from 0.5, scaled to 0-1
                confidence = float(abs(home_win_prob - 0.5) * 2)
                
            else:
                # Fallback for models without predict_proba
                prediction = model.predict(feature_array)
                home_win_prob = float(prediction[0])
                away_win_prob = 1.0 - home_win_prob
                confidence = 0.5
            
            # Calculate time (Acceptance Criteria: < 200ms)
            prediction_time_ms = (time.time() - start_time) * 1000
            
            # Log if slow
            if prediction_time_ms > 200: # pragma: no cover
                logger.warning(
                    f"SLOW: {version} took {prediction_time_ms:.2f}ms (target: <200ms)"
                )
            
            # Return result
            return {
                "home_win_probability": home_win_prob,
                "away_win_probability": away_win_prob,
                "confidence": confidence,
                "model_version": version,
                "prediction_time_ms": round(prediction_time_ms, 2)
            }
            
        except Exception as e:
            logger.error(f"Prediction failed for {version}: {e}")
            return {
                "error": str(e),
                "model_version": version
            }

    def prepare_features(
        self, 
        features: Dict[str, Any], 
        expected_features: Optional[List[str]] = None
    ) -> np.ndarray:
        """
        Convert feature dict to numpy array.
        
        Acceptance Criteria: Maps feature dict to numpy array.
        
        Args:
            features: Dict of feature names to values
            expected_features: List of features in expected order
            
        Returns:
            numpy array of shape (1, n_features)
        """
        try:
            if expected_features:
                # Use expected order
                feature_values = []
                
                for feature_name in expected_features:
                    if feature_name in features:
                        value = features[feature_name]
                    else:
                        # Missing feature - use 0.0
                        logger.warning(f"Missing feature: {feature_name}, using 0.0")
                        value = 0.0
                    
                    # Convert to float
                    try:
                        value = float(value)
                    except (ValueError, TypeError):
                        logger.warning(f"Invalid value for {feature_name}, using 0.0")
                        value = 0.0
                    
                    feature_values.append(value)
                
                # Convert to array (1, n_features)
                return np.array([feature_values], dtype=np.float32)
                
            else:
                # No expected features - use alphabetical order
                feature_names = sorted(features.keys())
                feature_values = [float(features[name]) for name in feature_names]
                return np.array([feature_values], dtype=np.float32)
            
        except Exception as e:
            logger.error(f"Failed to prepare features: {e}")
            return np.array([[]], dtype=np.float32)

    def get_loaded_models(self) -> List[str]:
        """Get list of loaded model versions."""
        return list(self.loaded_models.keys())

    def get_model_info(self, version: str) -> Optional[Dict[str, Any]]:
        """Get info about a specific model."""
        if version not in self.loaded_models:
            return None
        
        info = self.loaded_models[version].copy()
        info.pop('model', None)  # Remove model object
        info['metadata'] = self.model_metadata.get(version, {})
        return info