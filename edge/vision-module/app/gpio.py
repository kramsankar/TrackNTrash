"""GPIO relay control for the red light / gate hold. No-op when off-device."""
from __future__ import annotations


class Relay:
    def __init__(self, pin: int):
        self.pin = pin
        self._gpio = None
        try:
            import RPi.GPIO as GPIO  # type: ignore
            GPIO.setmode(GPIO.BCM)
            GPIO.setup(pin, GPIO.OUT)
            self._gpio = GPIO
        except Exception:
            self._gpio = None  # dev machine / edge without GPIO

    def pulse(self, seconds: float = 3.0) -> None:
        """Energize the relay (hold/red light) for `seconds`."""
        if self._gpio is None:
            print(f"[gpio] (simulated) relay pin {self.pin} ON for {seconds}s")
            return
        import time
        self._gpio.output(self.pin, self._gpio.HIGH)
        time.sleep(seconds)
        self._gpio.output(self.pin, self._gpio.LOW)
