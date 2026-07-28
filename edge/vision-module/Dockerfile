# Dock vision IoT Edge module image.
# Base includes Python; for GPU edge devices switch to an onnxruntime-gpu / CUDA base.
FROM python:3.11-slim

# OpenCV / zbar runtime libs
RUN apt-get update && apt-get install -y --no-install-recommends \
        libgl1 libglib2.0-0 libzbar0 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY app/ ./app/
COPY models/ ./models/

# IoT Edge sets the module environment; run as the module host.
CMD ["python", "-m", "app.main"]
