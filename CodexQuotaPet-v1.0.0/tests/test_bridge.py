import importlib.util
from pathlib import Path
import unittest
from unittest.mock import Mock

path=Path(__file__).resolve().parents[1]/'plugin/bridge.py'
spec=importlib.util.spec_from_file_location('bridge',path)
bridge=importlib.util.module_from_spec(spec)
spec.loader.exec_module(bridge)

class BridgeTests(unittest.TestCase):
    def test_initialize_auto_starts(self):
        starter=Mock(return_value='already_running')
        reply=bridge.dispatch({'id':1,'method':'initialize','params':{'protocolVersion':'2025-06-18'}},starter)
        starter.assert_called_once_with()
        self.assertEqual(reply['result']['protocolVersion'],'2025-06-18')
    def test_notifications_have_no_response_or_launch(self):
        starter=Mock()
        self.assertIsNone(bridge.dispatch({'method':'notifications/initialized'},starter))
        starter.assert_not_called()
    def test_startup_failure_does_not_break_codex(self):
        reply=bridge.dispatch({'id':2,'method':'initialize'},Mock(side_effect=FileNotFoundError()))
        self.assertIn('result',reply)
        self.assertIn('instructions',reply['result'])
    def test_tools_list_does_not_start(self):
        starter=Mock()
        reply=bridge.dispatch({'id':3,'method':'tools/list'},starter)
        self.assertEqual(reply['result']['tools'][0]['name'],'quota_pet_start')
        starter.assert_not_called()
    def test_explicit_start_is_idempotent(self):
        starter=Mock(return_value='already_running')
        reply=bridge.dispatch({'id':4,'method':'tools/call','params':{'name':'quota_pet_start','arguments':{}}},starter)
        starter.assert_called_once_with()
        self.assertFalse(reply['result'].get('isError',False))
    def test_unknown_tool_cannot_launch(self):
        starter=Mock()
        reply=bridge.dispatch({'id':5,'method':'tools/call','params':{'name':'other','arguments':{}}},starter)
        self.assertIn('error',reply)
        starter.assert_not_called()
    def test_no_caller_selected_executable(self):
        starter=Mock()
        reply=bridge.dispatch({'id':6,'method':'tools/call','params':{'name':'quota_pet_start','arguments':{'exe':'evil.exe'}}},starter)
        self.assertIn('error',reply)
        starter.assert_not_called()
    def test_ping_no_side_effect(self):
        starter=Mock()
        self.assertEqual(bridge.dispatch({'id':7,'method':'ping'},starter)['result'],{})
        starter.assert_not_called()

if __name__=='__main__': unittest.main()
