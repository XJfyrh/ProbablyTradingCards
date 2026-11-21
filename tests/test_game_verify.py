"""Exercise the agent CLI against a temporary Player.log and game mailbox."""

import json
from pathlib import Path
import subprocess
import sys
from tempfile import TemporaryDirectory
import time
import unittest
import uuid


CLI = Path(__file__).resolve().parents[1] / "scripts" / "game_verify.py"
CORE = "probably_trading_cards"
SLOT = 3
DAY = 8


class GameVerifyTest(unittest.TestCase):
    def setUp(self):
        temporary = TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.data = Path(temporary.name)
        self.bridge = self.data / "ModData" / CORE / "agent"
        self.bridge.mkdir(parents=True)
        self.session = uuid.uuid4().hex
        self.save_run_id = str(uuid.uuid4())

    def publish(self, filename, payload):
        path = self.bridge / filename
        temporary = path.with_suffix(".tmp")
        temporary.write_text(json.dumps(payload), encoding="utf-8")
        temporary.replace(path)
        return path

    def ready(self, **fields):
        return self.publish("status.json", {
            "protocol": 1, "session": self.session, "state": "ready",
            "heartbeat": int(time.time()), "slot": SLOT, "day": DAY, "runId": self.save_run_id,
            **fields,
        })

    def start(self, *arguments):
        process = subprocess.Popen(
            [sys.executable, "-X", "utf8", str(CLI), "--data-dir", str(self.data), *arguments],
            stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, encoding="utf-8",
        )

        def close():
            if process.poll() is None:
                process.kill()
                process.communicate(timeout=5)
            process.stdout.close()
            process.stderr.close()

        self.addCleanup(close)
        return process

    def finish(self, process, expected_code):
        output, errors = process.communicate(timeout=8)
        self.assertEqual(process.returncode, expected_code, output + errors)
        result = json.loads(output)
        self.assertIsInstance(result, dict)
        return result

    def request(self, process):
        path = self.bridge / "request.json"
        deadline = time.monotonic() + 5
        while not path.exists():
            if process.poll() is not None:
                self.fail("CLI exited without sending a request: " + " ".join(process.communicate()))
            if time.monotonic() >= deadline:
                self.fail("CLI did not send the request")
            time.sleep(0.02)
        request = json.loads(path.read_text(encoding="utf-8-sig"))
        self.assertEqual(request["protocol"], 1)
        self.assertEqual(request["session"], self.session)
        self.assertEqual(request["saveRunId"], self.save_run_id)
        self.assertTrue(request["id"])
        self.assertGreaterEqual(request["expires"], int(time.time()))
        return request

    def reply(self, request, **fields):
        return self.publish("result.json", {
            "protocol": 1, "id": request["id"], "session": request["session"],
            "action": request["action"], "status": "passed", "slot": SLOT, "day": DAY,
            **fields,
        })

    def test_audit_reads_native_mod_loader_lines(self):
        (self.data / "Player.log").write_text(
            "[ModLoader] audit: game 0.46D, trust list: issued 2026-11-16T19:03:25Z, "
            "0 blocked, 0 verified, kill switch off\n"
            f"[ModLoader] audit: {CORE} v0.5.0 source=Local workshop=0 owner=0 "
            f"running=True trust=Updated caps=none hash={'a' * 64}\n"
            "[ModLoader] audit: dock_letters v1.0.0 source=Local workshop=0 owner=0 "
            "running=False trust=NotChecked caps=declared none hash=-\n",
            encoding="utf-8",
        )
        core = self.finish(self.start("audit", CORE), 0)["mods"][CORE]
        self.assertEqual(core["source"], "Local")
        self.assertEqual(core["workshop"], 0)
        self.finish(self.start("audit", CORE, "dock_letters"), 1)
        self.finish(self.start("audit", "collection_bounty"), 1)

        with (self.data / "Player.log").open("a", encoding="utf-8") as log:
            log.write("[ModLoader] audit: game 0.46D, trust list: issued 2026-11-18T19:03:25Z\n"
                      "[ModLoader] audit: dock_letters v1.0.0 source=Local workshop=0 owner=0 "
                      "running=False trust=NotChecked caps=declared none hash=-\n")
        self.finish(self.start("audit", CORE), 1)

    def test_audit_reports_workshop_loads_without_requiring_local_source(self):
        (self.data / "Player.log").write_text(
            "[ModLoader] audit: game 0.46D, trust list: none\n"
            f"[ModLoader] audit: {CORE} v0.7.0 source=Workshop workshop=3842861920 owner=123 "
            f"running=True trust=Match caps=none hash={'b' * 64}\n", encoding="utf-8",
        )
        core = self.finish(self.start("audit", CORE), 0)["mods"][CORE]
        self.assertEqual(core["source"], "Workshop")
        self.assertEqual(core["workshop"], 3842861920)
        self.assertEqual(core["hash"], "b" * 64)

    def test_status_and_run_require_a_fresh_bridge(self):
        (self.data / "Player.log").write_text("NATIVE VALIDATION PASS 801\n", encoding="utf-8")
        self.finish(self.start("run", "--slot", str(SLOT), "--timeout", "1"), 2)
        self.assertFalse((self.bridge / "request.json").exists())
        self.ready()
        self.finish(self.start("status"), 0)
        self.ready(heartbeat=int(time.time()) - 3600)
        self.finish(self.start("status"), 2)
        self.finish(self.start("run", "--slot", str(SLOT), "--timeout", "1"), 2)
        self.assertFalse((self.bridge / "request.json").exists())

    def test_wrong_slot_does_not_send_a_request(self):
        self.ready()
        self.finish(self.start("run", "--slot", str(SLOT + 1), "--timeout", "1"), 2)
        self.assertFalse((self.bridge / "request.json").exists())

    def test_unidentified_save_does_not_receive_a_request(self):
        self.ready(runId="")
        self.finish(self.start("run", "--slot", str(SLOT), "--timeout", "1"), 2)
        self.assertFalse((self.bridge / "request.json").exists())

    def test_native_waits_for_its_own_result(self):
        self.ready()
        old = {"id": uuid.uuid4().hex, "session": self.session, "action": "native"}
        self.reply(old, checks=801)
        process = self.start("run", "--slot", str(SLOT), "--timeout", "4",
                             "--require-series", "dock_letters/dock_demo",
                             "--require-series", "harbor_post/dock_demo")
        request = self.request(process)
        self.assertEqual(request["action"], "native")
        self.assertEqual(request["slot"], SLOT)
        self.assertNotEqual(request["id"], old["id"])
        self.assertEqual(set(request["requireSeries"]), {"dock_letters/dock_demo", "harbor_post/dock_demo"})
        self.reply(request, checks=37)
        self.assertEqual(self.finish(process, 0)["checks"], 37)

    def test_snapshot_returns_the_game_state(self):
        self.ready()
        process = self.start("snapshot", "--slot", str(SLOT), "--timeout", "4")
        request = self.request(process)
        self.assertEqual(request["action"], "snapshot")
        self.assertEqual(request["slot"], SLOT)
        data = {"cash": 126, "items": [{"identifier": "dock_letters.ticket"}]}
        self.reply(request, data=data)
        self.assertEqual(self.finish(process, 0)["data"], data)

    def test_failed_assertion_has_failure_exit_code(self):
        self.ready()
        process = self.start("run", "--slot", str(SLOT), "--timeout", "4")
        request = self.request(process)
        self.reply(request, status="failed", checks=5, error="Expected credit balance 126")
        self.assertIn("126", str(self.finish(process, 1)))

    def test_two_agents_cannot_overwrite_one_request(self):
        self.ready()
        first = self.start("run", "--slot", str(SLOT), "--timeout", "4")
        request = self.request(first)
        self.finish(self.start("snapshot", "--timeout", "1"), 2)
        active = json.loads((self.bridge / "request.json").read_text(encoding="utf-8"))
        self.assertEqual(active, request)
        self.reply(request, checks=22)
        self.assertEqual(self.finish(first, 0)["checks"], 22)

    def test_previous_session_cannot_satisfy_a_request(self):
        self.ready()
        process = self.start("run", "--slot", str(SLOT), "--timeout", "1")
        request = self.request(process)
        self.reply(request, session=uuid.uuid4().hex, checks=801)
        self.finish(process, 2)

    def test_snapshot_from_another_slot_cannot_satisfy_a_request(self):
        self.ready()
        process = self.start("snapshot", "--slot", str(SLOT), "--timeout", "1")
        self.reply(self.request(process), slot=SLOT + 1, data={"cash": 100})
        self.finish(process, 2)

    def test_missing_reply_times_out(self):
        self.ready()
        self.finish(self.start("run", "--slot", str(SLOT), "--timeout", "1"), 2)

    def test_zero_checks_cannot_report_native_success(self):
        self.ready()
        process = self.start("run", "--slot", str(SLOT), "--timeout", "1")
        self.reply(self.request(process), checks=0)
        self.finish(process, 2)


if __name__ == "__main__":
    unittest.main()
