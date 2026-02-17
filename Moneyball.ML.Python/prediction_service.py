from flask import Flask, request, jsonify
import pickle
import numpy as np
import pandas as pd
from typing import Dict, List

app = Flask(__name__)

# Model registry - loaded at startup
models = {}

def load_models():
    """Load all active models from disk"""
    # Load from configuration
    models['nba_v1'] = pickle.load(open('models/nba_gradient_boost_v1.pkl', 'rb'))
    models['nba_v2'] = pickle.load(open('models/nba_neural_net_v2.pkl', 'rb'))

@app.route('/predict', methods=['POST'])
def predict():
    data = request.json
    model_name = data['model_name']
    features = data['features']  # Feature dict from .NET
    
    if model_name not in models:
        return jsonify({'error': 'Model not found'}), 404
    
    model = models[model_name]
    
    # Convert features to appropriate format
    X = prepare_features(features)
    
    # Generate prediction
    probabilities = model.predict_proba(X)[0]
    
    return jsonify({
        'home_win_probability': float(probabilities[1]),
        'away_win_probability': float(probabilities[0]),
        'confidence': float(np.max(probabilities))
    })

def prepare_features(features: Dict) -> np.ndarray:
    """Transform feature dict to model input format"""
    # Example features for NBA
    feature_vector = [
        features['home_elo_rating'],
        features['away_elo_rating'],
        features['home_rest_days'],
        features['away_rest_days'],
        features['home_win_streak'],
        features['away_win_streak'],
        # ... many more features
    ]
    return np.array(feature_vector).reshape(1, -1)

if __name__ == '__main__':
    load_models()
    app.run(host='localhost', port=5001)