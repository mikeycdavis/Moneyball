"""
Flask application for serving ML model predictions.
"""

from flask import Flask, request, jsonify
# from prediction_service import PredictionService

app = Flask(__name__)
# service = PredictionService()


@app.route('/predict/<version>', methods=['POST'])
def predict(version):
    """
    Prediction endpoint for a specific model version.
    
    Args:
        version: Model version identifier (e.g., 'v1', 'v2')
    
    Returns:
        JSON response with prediction results
    """
    data = request.get_json()
    
    # result = service.predict(version, data)
    
    # if result is None:
    #     return jsonify({"error": f"Model {version} not found"}), 404
        
    return jsonify({"status": "ok", "version": version}), 200


def main():
    """
    Main entry point for the application.
    This is called when running: moneyball-ml
    """
    print("Starting Moneyball ML Python service...")
    
    # Uncomment when prediction service is ready
    # service.load_models()
    
    app.run(debug=True, port=5001)


if __name__ == '__main__':
    main()