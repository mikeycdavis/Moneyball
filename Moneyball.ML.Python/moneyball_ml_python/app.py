"""
Flask application for serving ML model predictions.
"""

from flask import Flask, request, jsonify
from moneyball_ml_python.prediction.predict import PredictionService
import logging
import sys

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
    handlers=[logging.StreamHandler(sys.stdout)]
)
logger = logging.getLogger(__name__)

# Initialize Flask app
app = Flask(__name__)
app.config['JSON_SORT_KEYS'] = False

# Initialize prediction service
# These will be initialized in main()
prediction_service: PredictionService = PredictionService(models_dir="models")


@app.route('/predict/<version>', methods=['POST'])
def predict(version):
    """
    Prediction endpoint for a specific model version.
    
    Acceptance Criteria:
    - Returns probabilities in < 200ms
    - Returns 404 JSON if model not found
    
    Request body:
    {
        "features": {
            "home_offensive_rating": 115.2,
            "away_offensive_rating": 112.1,
            ...
        }
    }
    
    Response (200 OK):
    {
        "home_win_probability": 0.68,
        "away_win_probability": 0.32,
        "confidence": 0.85,
        "model_version": "v1",
        "prediction_time_ms": 45.3
    }
    
    Response (404 Not Found):
    {
        "error": "Model v99 not found",
        "available_models": ["v1", "v2", "v3"]
    }
    """
    if request.content_length in (None, 0):
        return jsonify({'error': 'Missing request body'}), 400

    # Get data from request body
    data = request.get_json()
    
    if not data or 'features' not in data:
        return jsonify({'error': 'Missing features in request body'}), 400

    # Get features from data
    features = data['features']
    
    if not features:
        return jsonify({'error': 'Missing features in request body'}), 400
    
    # Make prediction
    result = prediction_service.predict(version, features)
    
    # Check if model was found (Acceptance Criteria: 404 if not found)
    if result is None:
        return jsonify({
            "error": f"Model {version} not found",
            "available_models": prediction_service.get_loaded_models()
        }), 404
    
    # Return prediction result (Acceptance Criteria: < 200ms)
    return jsonify(result), 200


@app.route('/models', methods=['GET'])
def list_models():
    """
    List all loaded models.
    
    Response:
    {
        "models": ["v1", "v2", "NBA_LogReg_v1"],
        "count": 3
    }
    """
    models = prediction_service.get_loaded_models()
    return jsonify({
        "models": models,
        "count": len(models)
    }), 200


@app.route('/models/<version>', methods=['GET'])
def get_model(version):
    """
    Get information about a specific model.
    
    Response (200 OK):
    {
        "path": "/path/to/model.pkl",
        "loaded_at": 1234567890.123,
        "metadata": {
            "is_active": true,
            "expected_features": [...],
            "description": "..."
        }
    }
    
    Response (404 Not Found):
    {
        "error": "Model v99 not found"
    }
    """
    info = prediction_service.get_model_info(version)
    
    if info is None:
        return jsonify({
            'error': f'Model {version} not found',
            'available_models': prediction_service.get_loaded_models()
        }), 404
    
    return jsonify(info), 200


@app.route('/health', methods=['GET'])
def health():
    """
    Health check endpoint.
    
    Response:
    {
        "status": "healthy",
        "service": "moneyball-ml-python",
        "models_loaded": 3
    }
    """
    return jsonify({
        "status": "healthy",
        "service": "moneyball-ml-python",
        'models_loaded': len(prediction_service.get_loaded_models()) if prediction_service else 0
    }), 200


def main():
    """
    Main entry point for the application.
    Called when running: moneyball-ml
    
    Acceptance Criteria: Loads all IsActive models at startup
    """
    logger.info("Starting Moneyball ML Python service...") # pragma: no cover
    
    # Load models at startup (Acceptance Criteria)
    prediction_service.load_models()
    
    # Run Flask server
    logger.info("Starting Flask server on port 5001...") # pragma: no cover
    app.run(
        host="0.0.0.0",
        port=5001,
        debug=True
    )


if __name__ == '__main__':
    main()