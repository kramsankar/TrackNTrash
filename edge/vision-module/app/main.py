"""IoT Edge module host: wires the pipeline to IoT Hub, the module twin, and the trigger.

Runs on the edge device. Off-device (no azure-iot-device), import guards keep it inert so the
pure pipeline can still be exercised by tests.
"""
from __future__ import annotations

import asyncio
import json
import os
import uuid

from .config import ModuleConfig
from .detector import Yolov8OnnxDetector
from .gpio import Relay
from .manifest_cache import ManifestCache
from .pipeline import DockPipeline, RtspFrameSource
from .verdict import Verdict


async def main() -> None:
    from azure.iot.device.aio import IoTHubModuleClient  # deferred (edge-only)

    client = IoTHubModuleClient.create_from_edge_environment()
    await client.connect()

    twin = await client.get_twin()
    config = ModuleConfig.from_twin(twin.get("desired", {}))

    manifests = ManifestCache(sync_url=config.manifest_sync_url)
    manifests.sync()

    pipeline = DockPipeline(
        config=config,
        source=RtspFrameSource(config.camera_rtsp_url),
        detector=Yolov8OnnxDetector(config.model_path, config.confidence),
        manifests=manifests,
    )
    relay = Relay(config.gpio_relay_pin)

    async def twin_patch_handler(patch):
        nonlocal config
        config = ModuleConfig.from_twin({**twin.get("desired", {}), **patch})
        print("[twin] config updated")

    client.on_twin_desired_properties_patch_received = twin_patch_handler

    async def on_trigger(_):
        await run_verification(client, pipeline, relay, config)

    # Direct method 'trigger' (operator button) also invokes a verification.
    client.on_method_request_received = lambda req: asyncio.create_task(
        _handle_method(client, req, pipeline, relay, config))

    print("[dock] module started; awaiting triggers")
    while True:
        # Motion-based trigger loop would hook a motion detector here; poll placeholder.
        await asyncio.sleep(1)


async def _handle_method(client, request, pipeline, relay, config):
    from azure.iot.device import MethodResponse
    if request.name == "trigger":
        await run_verification(client, pipeline, relay, config)
        await client.send_method_response(MethodResponse.create_from_method_request(request, 200, {"status": "ok"}))
    else:
        await client.send_method_response(MethodResponse.create_from_method_request(request, 404, {}))


async def run_verification(client, pipeline: DockPipeline, relay: Relay, config: ModuleConfig) -> None:
    event, frames = pipeline.run_once(client_event_id=str(uuid.uuid4()))

    if event.verdict is not Verdict.PASS:
        frame_ref = _save_and_upload(frames, event, config)
        event.frame_ref = frame_ref
        relay.pulse(3.0)

    msg = json.dumps(event.to_message())
    from azure.iot.device import Message
    await client.send_message_to_output(Message(msg), "dockVerification")
    print(f"[dock] {event.verdict.value} tray={event.tray_qr} "
          f"decoded={event.to_message()['decodedCount']} detected={event.detected_count} "
          f"expected={event.expected_count}")


def _save_and_upload(frames, event, config: ModuleConfig) -> str | None:
    """Annotate the best frame, save locally, upload to Blob. Returns the blob path/ref."""
    if not frames:
        return None
    try:
        import cv2
        annotated = frames[0].copy()
        label = f"{event.verdict.value} d={len(event.decoded_cartons)} y={event.detected_count} e={event.expected_count}"
        cv2.putText(annotated, label, (20, 40), cv2.FONT_HERSHEY_SIMPLEX, 1.0, (0, 0, 255), 2)
        fname = f"dock/{event.tray_qr or 'unknown'}/frame-{uuid.uuid4()}.jpg"
        local = os.path.join("/tmp", os.path.basename(fname))
        cv2.imwrite(local, annotated)
        _upload_blob(local, fname, config.blob_exception_container)
        return f"{config.blob_exception_container}/{fname}"
    except Exception as ex:  # never let annotation failure block the verdict
        print(f"[dock] frame save/upload failed: {ex}")
        return None


def _upload_blob(local_path: str, blob_name: str, container: str) -> None:
    conn = os.environ.get("BLOB_CONNECTION_STRING")
    if not conn:
        print(f"[dock] (no BLOB_CONNECTION_STRING) would upload {blob_name}")
        return
    from azure.storage.blob import BlobServiceClient
    svc = BlobServiceClient.from_connection_string(conn)
    with open(local_path, "rb") as f:
        svc.get_blob_client(container=container, blob=blob_name).upload_blob(f, overwrite=True)


if __name__ == "__main__":
    asyncio.run(main())
