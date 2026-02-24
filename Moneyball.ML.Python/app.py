from flask import Flask, request, jsonify
from prediction_service import PredictionService

app = Flask(__name__)
service = PredictionService()

@app.route('/predict/<version>', methods=['POST'])
def predict(version):
    data = request.get_json()
    
    result = service.predict(version, data)
    
    if result is None:
        return jsonify({"error": f"Model {version} not found"}), 404
        
    return jsonify(result), 200

if __name__ == '__main__':
    service.load_models()
    app.run(debug=True, port=5001)
