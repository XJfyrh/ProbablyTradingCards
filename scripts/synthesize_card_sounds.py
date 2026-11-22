"""Render the prototype card sound effects for comparison."""

from array import array
from math import exp, sin, tau
from pathlib import Path
from random import Random
import sys
import wave


SAMPLE_RATE = 44_100
OUTPUT = Path(__file__).resolve().parents[1] / "artifacts" / "synthesized-card-sounds"


class Sound:
    def __init__(self, duration, seed):
        self.samples = [0.0] * round(duration * SAMPLE_RATE)
        self.random = Random(seed)

    def noise(self, start, length, gain, low_hz, high_hz, attack):
        low_pass = 0.0
        slow_pass = 0.0
        low_coeff = 1.0 - exp(-tau * high_hz / SAMPLE_RATE)
        slow_coeff = 1.0 - exp(-tau * low_hz / SAMPLE_RATE)
        offset = round(start * SAMPLE_RATE)
        for index in range(round(length * SAMPLE_RATE)):
            time = index / SAMPLE_RATE
            noise = self.random.uniform(-1.0, 1.0)
            low_pass += low_coeff * (noise - low_pass)
            slow_pass += slow_coeff * (low_pass - slow_pass)
            envelope = min(1.0, time / attack) * (1.0 - time / length) ** 1.7
            self.samples[offset + index] += gain * (low_pass - slow_pass) * envelope

    def tone(self, start, length, gain, hz, overtone=0.0):
        offset = round(start * SAMPLE_RATE)
        for index in range(round(length * SAMPLE_RATE)):
            time = index / SAMPLE_RATE
            phase = tau * hz * time
            envelope = min(1.0, time / 0.003) * (1.0 - time / length) ** 2.7
            value = sin(phase) + overtone * sin(phase * 2.03)
            self.samples[offset + index] += gain * value * envelope

    def save(self, name, peak):
        scale = peak * 32767 / max(abs(sample) for sample in self.samples)
        pcm = array("h", (round(sample * scale) for sample in self.samples))
        if sys.byteorder == "big":
            pcm.byteswap()
        with wave.open(str(OUTPUT / (name + ".wav")), "wb") as wav:
            wav.setparams((1, 2, SAMPLE_RATE, len(pcm), "NONE", "not compressed"))
            wav.writeframes(pcm.tobytes())


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)

    tear = Sound(0.208, 418)
    tear.noise(0.003, 0.065, 0.28, 580, 4600, 0.015)
    tear.noise(0.048, 0.107, 0.80, 360, 6900, 0.007)
    for time, gain in [(0.051, 0.35), (0.064, 0.6), (0.078, 0.9),
                       (0.096, 0.68), (0.114, 0.73), (0.133, 0.32)]:
        tear.noise(time, 0.014, gain, 1480, 9000, 0.001)
    tear.noise(0.147, 0.054, 0.32, 580, 4500, 0.002)
    tear.save("foil_tear", 0.56)

    cut = Sound(0.142, 878)
    cut.noise(0.001, 0.022, 0.30, 1500, 9000, 0.001)
    cut.tone(0.001, 0.019, 0.06, 1860, 0.35)
    cut.noise(0.012, 0.093, 0.55, 1350, 7500, 0.011)
    cut.noise(0.056, 0.031, 0.43, 2340, 10400, 0.002)
    cut.noise(0.092, 0.040, 0.14, 510, 4350, 0.002)
    cut.save("foil_cut", 0.40)

    land = Sound(0.085, 847)
    land.noise(0.001, 0.044, 0.15, 2450, 7300, 0.016)
    land.noise(0.022, 0.039, 0.56, 720, 5100, 0.001)
    land.tone(0.022, 0.058, 0.085, 360)
    land.tone(0.024, 0.043, 0.023, 638)
    land.save("card_land", 0.28)

    rare = Sound(0.138, 93)
    rare.tone(0.002, 0.13, 1.0, 830.6, 0.14)
    rare.tone(0.004, 0.065, 0.085, 1264)
    rare.save("card_rare", 0.17)

    super_rare = Sound(0.31, 419)
    super_rare.tone(0.001, 0.196, 0.64, 622.3, 0.12)
    super_rare.tone(0.038, 0.260, 1.0, 932.3, 0.095)
    super_rare.tone(0.040, 0.152, 0.105, 1408)
    super_rare.save("card_super_rare", 0.25)

    page = Sound(0.225, 614)
    page.noise(0.002, 0.099, 0.29, 820, 2860, 0.008)
    page.noise(0.046, 0.146, 0.86, 320, 4400, 0.044)
    page.noise(0.123, 0.064, 0.32, 960, 5400, 0.012)
    page.noise(0.169, 0.049, 0.17, 520, 3560, 0.0015)
    page.save("album_page", 0.24)

    file_card = Sound(0.167, 275)
    file_card.noise(0.002, 0.117, 0.48, 810, 4500, 0.015)
    file_card.noise(0.037, 0.080, 0.32, 2180, 6910, 0.017)
    file_card.noise(0.091, 0.054, 0.51, 420, 3140, 0.0025)
    file_card.tone(0.101, 0.048, 0.068, 330)
    file_card.save("album_file", 0.23)

    display = Sound(0.110, 452)
    display.noise(0.000, 0.036, 0.55, 670, 6200, 0.001)
    display.tone(0.005, 0.092, 0.18, 1340)
    display.tone(0.007, 0.060, 0.11, 2165)
    display.noise(0.017, 0.043, 0.13, 1000, 6290, 0.001)
    display.save("display_place", 0.22)


if __name__ == "__main__":
    main()
