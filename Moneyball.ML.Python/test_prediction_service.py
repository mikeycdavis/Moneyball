import pytest
from app import app

@pytest.fixture
def client():
    with app.test_client() as client:
        yield client

def test_predict_success(client):
    """Test valid model version returns 200."""
    response = client.post('/predict/v1', json={"f1": 1})
    assert response.status_code == 200
    assert "probability" in response.get_json()

def test_model_not_found(client):
    """Test invalid model version returns 404."""
    response = client.post('/predict/v99', json={"f1": 1})
    assert response.status_code == 404
    assert response.get_json()["error"] == "Model v99 not found"
