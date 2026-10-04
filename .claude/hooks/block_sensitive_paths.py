import json
import re
import sys

RAW_DATA_PATTERN = re.compile(r"(?:^|[\s/'\"=:(])data/raw(?:/|$|[\s'\";|&)])")
ENV_FILE_PATTERN = re.compile(r"(?:^|[\s/'\"=:(])\.env(?:\.(?!example\b)[\w.-]+)?(?:$|[\s'\";|&)])")
INSPECTED_FIELDS = ("file_path", "notebook_path", "path", "glob", "command")
DENY_REASON = (
    "Bị chặn theo CLAUDE.md quy tắc 4 và quyết định A3: không đọc, ghi hay chạy lệnh chạm tới data/raw/ "
    "(dữ liệu gốc có thể chứa thông tin cá nhân) hoặc file .env (bí mật). "
    "Nếu cần xử lý data/raw/, người dùng tự chạy lệnh trong terminal của mình."
)


def touches_sensitive_path(tool_input: dict[str, object]) -> bool:
    values = (str(tool_input.get(field, "")) for field in INSPECTED_FIELDS)
    return any(RAW_DATA_PATTERN.search(value) or ENV_FILE_PATTERN.search(value) for value in values)


def decision(payload: dict[str, object]) -> dict[str, object] | None:
    tool_input = payload.get("tool_input")
    if not isinstance(tool_input, dict) or not touches_sensitive_path(tool_input):
        return None
    return {
        "hookSpecificOutput": {
            "hookEventName": "PreToolUse",
            "permissionDecision": "deny",
            "permissionDecisionReason": DENY_REASON,
        }
    }


def main() -> int:
    try:
        payload = json.load(sys.stdin)
    except json.JSONDecodeError:
        return 0
    result = decision(payload)
    if result is not None:
        print(json.dumps(result, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    sys.exit(main())
