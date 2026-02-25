"""
Flask application for serving ML model predictions.
"""

from flask import Flask, request, jsonify
from prediction_service import PredictionService
import logging

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
)
logger = logging.getLogger(__name__)

# Initialize Flask app
app = Flask(__name__)

# Initialize prediction service
service = PredictionService(models_dir="models")


@app.route('/predict/<version>', methods=['POST'])
def predict(version):
    """
    Prediction endpoint for a specific model version.
    
    Acceptance Criteria:
    - Returns probabilities in < 200ms
    - Returns 404 JSON if model not found
    
    Request body:
    {
        "HomeWinRate": 0.65,
        "AwayWinRate": 0.35,
        "HomePointsAvg": 110.0,
        "AwayPointsAvg": 95.0
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
    # Get features from request body
    features = request.get_json()
    
    if not features:
        return jsonify({"error": "No features provided"}), 400
    
    # Make prediction
    result = service.predict(version, features)
    
    # Check if model was found (Acceptance Criteria: 404 if not found)
    if result is None:
        return jsonify({
            "error": f"Model {version} not found",
            "available_models": service.get_loaded_models()
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
    models = service.get_loaded_models()
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
    info = service.get_model_info(version)
    
    if info is None:
        return jsonify({"error": f"Model {version} not found"}), 404
    
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
        "models_loaded": len(service.get_loaded_models())
    }), 200


def main():
    """
    Main entry point for the application.
    Called when running: moneyball-ml
    
    Acceptance Criteria: Loads all IsActive models at startup
    """
    logger.info("Starting Moneyball ML Python service...")
    
    # Load models at startup (Acceptance Criteria)
    service.load_models()
    
    # Run Flask server
    app.run(
        host="0.0.0.0",
        port=5001,
        debug=True
    )


if __name__ == '__main__':
    main()