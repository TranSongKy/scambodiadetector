import csv
import json
import tempfile
import threading
import unittest
import unittest.mock
from http.server import BaseHTTPRequestHandler, HTTPServer
from pathlib import Path

from export_reports import ExportError, build_url, fetch_reports, main, to_review_row

API_KEY = "test-key"
REPORT = {
    "id": "0199a0c0-0000-7000-8000-000000000001",
    "text": "Goi <PHONE> ngay",
    "label": "scam",
    "channel": "telegram",
    "createdAt": "2026-10-02T08:00:00+00:00",
}


class FakeReportsHandler(BaseHTTPRequestHandler):
    received_paths: list[str] = []

    def log_message(self, format: str, *args: object) -> None:
        pass

    def do_GET(self) -> None:
        FakeReportsHandler.received_paths.append(self.path)
        if self.headers.get("X-Api-Key") != API_KEY:
            self.send_response(401)
            self.end_headers()
            return
        body = json.dumps([REPORT]).encode("utf-8")
        self.send_response(200)
        self.send_header("content-type", "application/json")
        self.end_headers()
        self.wfile.write(body)


class ExportReportsTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.server = HTTPServer(("127.0.0.1", 0), FakeReportsHandler)
        cls.base_url = f"http://127.0.0.1:{cls.server.server_port}"
        threading.Thread(target=cls.server.serve_forever, daemon=True).start()

    @classmethod
    def tearDownClass(cls) -> None:
        cls.server.shutdown()

    def test_build_url_includes_limit_and_optional_since(self) -> None:
        self.assertEqual("http://api/api/v1/reports?limit=10", build_url("http://api/", None, 10))
        self.assertEqual(
            "http://api/api/v1/reports?limit=10&since=2026-10-01T00%3A00%3A00Z",
            build_url("http://api", "2026-10-01T00:00:00Z", 10),
        )

    def test_review_row_maps_channel_source_and_date(self) -> None:
        row = to_review_row(REPORT)

        self.assertEqual("telegram", row["channel"])
        self.assertEqual("contributed", row["source"])
        self.assertEqual("2026-10-02", row["collected_at"])
        self.assertEqual("", row["annotator"])

    def test_unknown_channel_maps_to_other(self) -> None:
        self.assertEqual("other", to_review_row({**REPORT, "channel": "extension"})["channel"])

    def test_fetch_with_wrong_key_raises(self) -> None:
        with self.assertRaises(ExportError):
            fetch_reports(build_url(self.base_url, None, 5), "wrong")

    def test_main_writes_review_csv(self) -> None:
        environment = {"REPORTS_API_KEY": API_KEY}
        with tempfile.TemporaryDirectory() as directory, unittest.mock.patch.dict("os.environ", environment):
            output = Path(directory) / "review.csv"

            exit_code = main([str(output), "--api", self.base_url, "--limit", "5"])

            with output.open(encoding="utf-8", newline="") as output_file:
                rows = list(csv.DictReader(output_file))
        self.assertEqual(0, exit_code)
        self.assertEqual("Goi <PHONE> ngay", rows[0]["text"])

    def test_main_without_api_key_fails(self) -> None:
        with unittest.mock.patch.dict("os.environ", {}, clear=True):
            self.assertEqual(1, main(["out.csv"]))


if __name__ == "__main__":
    unittest.main()
