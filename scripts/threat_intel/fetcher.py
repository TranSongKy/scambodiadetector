import time
import urllib.error
import urllib.request
import urllib.robotparser
from collections.abc import Callable
from urllib.parse import urlsplit

USER_AGENT = "ScambodiaDetectorBot/1.0 (+https://github.com/TranSongKy/scambodiadetector)"
REQUEST_TIMEOUT_SECONDS = 20
MAX_RESPONSE_BYTES = 3_000_000
DEFAULT_DELAY_SECONDS = 2.0


class FetchError(Exception):
    pass


class PoliteFetcher:
    def __init__(
        self,
        delay_seconds: float = DEFAULT_DELAY_SECONDS,
        opener: Callable[[urllib.request.Request], bytes] | None = None,
        sleeper: Callable[[float], None] = time.sleep,
    ) -> None:
        self._delay_seconds = delay_seconds
        self._open = opener or self._open_url
        self._sleep = sleeper
        self._robots: dict[str, urllib.robotparser.RobotFileParser | None] = {}
        self._last_request_by_host: dict[str, float] = {}

    def fetch(self, url: str) -> str:
        if not self._allowed_by_robots(url):
            raise FetchError(f"robots.txt không cho phép: {url}")
        self._wait_for_host(urlsplit(url).netloc)
        request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
        return self._open(request).decode("utf-8", errors="replace")

    def _wait_for_host(self, host: str) -> None:
        elapsed = time.monotonic() - self._last_request_by_host.get(host, float("-inf"))
        if elapsed < self._delay_seconds:
            self._sleep(self._delay_seconds - elapsed)
        self._last_request_by_host[host] = time.monotonic()

    def _allowed_by_robots(self, url: str) -> bool:
        parts = urlsplit(url)
        origin = f"{parts.scheme}://{parts.netloc}"
        if origin not in self._robots:
            self._robots[origin] = self._load_robots(origin)
        robots = self._robots[origin]
        return robots is None or robots.can_fetch(USER_AGENT, url)

    def _load_robots(self, origin: str) -> urllib.robotparser.RobotFileParser | None:
        try:
            content = self._open(urllib.request.Request(f"{origin}/robots.txt", headers={"User-Agent": USER_AGENT}))
        except FetchError:
            return None
        robots = urllib.robotparser.RobotFileParser()
        robots.parse(content.decode("utf-8", errors="replace").splitlines())
        return robots

    @staticmethod
    def _open_url(request: urllib.request.Request) -> bytes:
        try:
            with urllib.request.urlopen(request, timeout=REQUEST_TIMEOUT_SECONDS) as response:
                return response.read(MAX_RESPONSE_BYTES)
        except (urllib.error.URLError, TimeoutError, ValueError) as error:
            raise FetchError(f"Không tải được {request.full_url}: {error}") from error
