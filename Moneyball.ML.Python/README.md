# Moneyball.ML.Python

Sports betting ML model server using Flask.

## Project Structure

```
Moneyball.ML.Python/
├── pyproject.toml              # Modern Python project configuration (PEP 621)
├── README.md                   # This file
├── .gitignore                  # Git ignore patterns
├── moneyball_ml_python/       # Main package (underscore in folder name)
│   ├── __init__.py            # Package initialization
│   └── app.py                 # Flask application with main() entry point
├── tests/                     # Test files
│   ├── __init__.py
│   └── test_app.py
└── dist/                      # Build artifacts (created by build command)
```

## Important: Package Naming

- **PyPI package name**: `moneyball-ml-python` (uses dashes) - in pyproject.toml
- **Python package folder**: `moneyball_ml_python` (uses underscores) - actual folder name
- **Import statement**: `import moneyball_ml_python` (uses underscores)
- **Console command**: `moneyball-ml` (uses dashes) - after installation

This is Python's standard convention.

## Setup

### 1. Install build dependencies

```bash
pip install build
```

That's it! The `build` package is all you need to create wheel and sdist.

### 2. Build the package

```bash
python -m build
```

This creates both:
- **Wheel**: `dist/moneyball_ml_python-0.1.0-py3-none-any.whl`
- **Source distribution**: `dist/moneyball-ml-python-0.1.0.tar.gz`

### 3. Install the package

```bash
# Install in editable mode (for development)
pip install -e .

# Or install the built wheel
pip install dist/moneyball_ml_python-0.1.0-py3-none-any.whl
```

### 4. Install with dev dependencies

```bash
pip install -e ".[dev]"
```

## Usage

### Run as a console command (after installation)

```bash
moneyball-ml
```

### Run directly with Python

```bash
python -m moneyball_ml_python.app
```

### Run the app.py file directly

```bash
cd moneyball_ml_python
python app.py
```

## API Endpoints

### Health Check
```bash
curl http://localhost:5000/health
```

### Prediction
```bash
curl -X POST http://localhost:5000/predict \
  -H "Content-Type: application/json" \
  -d '{
    "model_name": "NBA_LogReg_v1",
    "features": {
      "HomeWinRate": 0.65,
      "AwayWinRate": 0.35
    }
  }'
```

## Testing

```bash
pytest
```

## CI/CD Build Command

In your CI pipeline, use:

```bash
# Install build tool
pip install build

# Create wheel and sdist
python -m build

# Artifacts will be in dist/
ls -la dist/
```

## Dependencies

### Runtime (automatically installed with package)
- flask>=3.0.0
- requests>=2.31.0
- pandas>=2.0.0
- numpy>=1.24.0
- scikit-learn>=1.3.0
- joblib>=1.3.0

### Development (install with `pip install -e ".[dev]"`)
- pytest>=8.0.0
- pytest-cov>=5.0.0

### Build-only (needed for `python -m build`)
- build

## What You Need to Install

**For building the package:**
```bash
pip install build
```

**For development:**
```bash
pip install -e ".[dev]"
```

That's it! Everything else is handled automatically by pyproject.toml.