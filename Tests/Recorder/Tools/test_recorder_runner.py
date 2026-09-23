"""External-fixture orchestration regression; no phone or browser required."""
import contextlib
import io
import json
from pathlib import Path
import runpy
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
from uuid import UUID


class ExternalRunnerTests(unittest.TestCase):
    run_id = UUID(int=42).hex

    def report(self, mode='audio-external', **values):
        return dict(runId=self.run_id, mode=mode, completed=False,
                    takes=[dict(videoBackend='HoloKit')], **values)

    def run_fixture(self, reports, mode='audio-external', validation=0):
        calls = []
        output = io.StringIO()
        with tempfile.TemporaryDirectory() as directory:
            def command(arguments, **kwargs):
                calls.append(arguments)
                self.assertNotIn('--console', arguments)
                if 'from' in arguments:
                    report = reports.pop(0)
                    destination = Path(arguments[arguments.index('--destination') + 1])
                    destination.write_text(json.dumps(report) if isinstance(report, dict) else report)
                return subprocess.CompletedProcess(arguments, 0)

            argv = ['run_recorder_probe.py', '--device', 'test-device', '--bundle-id', 'test-app',
                    '--mode', mode, '--output', directory]
            with patch.object(sys, 'argv', argv), patch('uuid.uuid4', return_value=UUID(int=42)), \
                 patch('subprocess.run', side_effect=command), \
                 patch('subprocess.Popen', side_effect=AssertionError('External runner must not own/terminate a console process')), \
                 patch('subprocess.call', return_value=validation) as collect, \
                 patch('time.sleep'), contextlib.redirect_stdout(output):
                try:
                    runpy.run_path(str(Path(__file__).with_name('run_recorder_probe.py')), run_name='__main__')
                except (SystemExit, RuntimeError) as result:
                    return result, output.getvalue(), collect.call_count, calls
        self.fail('Runner did not return a result')

    def test_waits_for_current_recording_and_ignores_partial_write(self):
        ready = self.report()
        done = dict(ready, completed=True)
        result, output, collected, calls = self.run_fixture([
            dict(runId='old', completed=True), '{partial', ready, ready, done])
        self.assertEqual(result.code, 0)
        self.assertEqual(output.count('EXTERNAL-READY:'), 1)
        self.assertLess(output.index('EXTERNAL-READY:'), output.index('EXTERNAL-COMPLETE:'))
        self.assertEqual(collected, 1)
        self.assertEqual(sum('launch' in call for call in calls), 1)

    def test_interruption_collects_failure_without_restarting(self):
        result, output, collected, calls = self.run_fixture([self.report(interrupted=True)], validation=1)
        self.assertEqual(result.code, 1)
        self.assertIn('EXTERNAL-INTERRUPTED:', output)
        self.assertNotIn('EXTERNAL-READY:', output)
        self.assertEqual(collected, 1)
        self.assertEqual(sum('launch' in call for call in calls), 1)

    def test_wrong_mode_is_not_accepted_as_reference(self):
        result, _, collected, _ = self.run_fixture([self.report()], mode='audio-external-reference')
        self.assertIsInstance(result, RuntimeError)
        self.assertEqual(collected, 0)

    def test_reference_requires_native_backend(self):
        result, output, collected, _ = self.run_fixture([
            self.report(mode='audio-external-reference')], mode='audio-external-reference')
        self.assertIsInstance(result, RuntimeError)
        self.assertNotIn('EXTERNAL-READY:', output)
        self.assertEqual(collected, 0)


if __name__ == '__main__':
    unittest.main()
