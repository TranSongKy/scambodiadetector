import importlib.util
import unittest
from pathlib import Path

HOOK_PATH = Path(__file__).resolve().parents[2] / ".claude" / "hooks" / "block_sensitive_paths.py"
SPEC = importlib.util.spec_from_file_location("block_sensitive_paths", HOOK_PATH)
hook = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(hook)
RAW = "data/" + "raw"


def deny(tool_name: str, **tool_input: str) -> bool:
    return hook.decision({"tool_name": tool_name, "tool_input": tool_input}) is not None


class SensitivePathHookTests(unittest.TestCase):
    def test_raw_data_access_is_denied(self) -> None:
        self.assertTrue(deny("Read", file_path=f"/repo/{RAW}/batch.csv"))
        self.assertTrue(deny("Glob", pattern="*.csv", path=f"/repo/{RAW}"))
        self.assertTrue(deny("Bash", command=f"head {RAW}/batch.csv"))
        self.assertTrue(deny("Write", file_path=f"{RAW}/new.csv", content="x"))

    def test_env_files_are_denied_but_example_is_allowed(self) -> None:
        self.assertTrue(deny("Read", file_path="/repo/.env"))
        self.assertTrue(deny("Bash", command="cat .env.local"))
        self.assertFalse(deny("Read", file_path="/repo/.env.example"))
        self.assertFalse(deny("Read", file_path="/repo/src/.environment.cs"))

    def test_other_paths_and_text_searches_are_allowed(self) -> None:
        self.assertFalse(deny("Read", file_path="/repo/data/processed/dataset.csv"))
        self.assertFalse(deny("Grep", pattern=RAW, path="/repo/docs"))
        self.assertFalse(deny("Bash", command="ls data"))
        self.assertIsNone(hook.decision({"tool_name": "Bash"}))

    def test_denial_uses_pre_tool_use_permission_decision(self) -> None:
        result = hook.decision({"tool_name": "Read", "tool_input": {"file_path": f"{RAW}/a.csv"}})

        self.assertEqual("deny", result["hookSpecificOutput"]["permissionDecision"])
        self.assertIn(RAW, result["hookSpecificOutput"]["permissionDecisionReason"])
