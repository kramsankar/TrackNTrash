# Dock vision IoT Edge module image.
#
# Two stages so the compiler toolchain and pip's caches stay out of the shipped layer. The
# large win, though, was dropping ultralytics from requirements.txt — it pulled torch in for a
# dependency only the model *export* needs, and the image was 3.1 GB. Inference runs on
# onnxruntime directly.
#
# For a GPU gateway, swap onnxruntime for onnxruntime-gpu and use a CUDA base; detector.py
# already prefers CUDAExecutionProvider when the runtime offers it.

# ---------- build ----------
FROM python:3.11-slim AS build

# pyzbar and zxing-cpp may build from source on architectures without a wheel.
RUN apt-get update && apt-get install -y --no-install-recommends \
        build-essential \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt /tmp/requirements.txt
RUN python -m venv /opt/venv \
    && /opt/venv/bin/pip install --no-cache-dir --upgrade pip wheel \
    && /opt/venv/bin/pip install --no-cache-dir -r /tmp/requirements.txt \
    # Shipping .pyc for code that runs once per boot is not worth the layer size.
    && find /opt/venv -name '__pycache__' -type d -prune -exec rm -rf {} + \
    && find /opt/venv -name '*.pyc' -delete \
    # onnxruntime and opencv ship test suites and bundled headers the module never uses.
    && find /opt/venv -type d -name 'tests' -prune -exec rm -rf {} + \
    && find /opt/venv -type d -name 'include' -path '*onnxruntime*' -prune -exec rm -rf {} +

# ---------- runtime ----------
FROM python:3.11-slim

# OpenCV and zbar shared libraries only — no compiler.
RUN apt-get update && apt-get install -y --no-install-recommends \
        libgl1 libglib2.0-0 libzbar0 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /opt/venv /opt/venv

ENV PATH="/opt/venv/bin:$PATH" \
    PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    OPENCV_VIDEOIO_PRIORITY_MSMF=0

WORKDIR /app
COPY app/ ./app/
COPY models/ ./models/

# IoT Edge sets the module environment; run as the module host.
CMD ["python", "-m", "app.main"]
