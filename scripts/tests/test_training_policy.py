import unittest

from training_data.policy import decide_training_use

VOZ_ROBOTS = """User-agent: *
Content-Signal: search=yes,ai-train=no,use=reference
Allow: /

User-agent: GPTBot
Disallow: /
"""
TINHTE_ROBOTS = """User-agent: *
Disallow: /account/
Allow: /
User-agent: Bytespider
Disallow: /
User-agent: ClaudeBot
Disallow: /
"""
OPEN_ROBOTS = """User-agent: *
Disallow: /api/
Allow: /
"""
PARTIAL_AI_BLOCK = """User-agent: GPTBot
Disallow: /private/
"""


class TrainingPolicyTests(unittest.TestCase):
    def test_content_signal_ai_train_no_forbids(self) -> None:
        decision = decide_training_use(VOZ_ROBOTS)

        self.assertFalse(decision.allowed)
        self.assertIn("ai-train=no", decision.reason)

    def test_fully_blocked_ai_crawler_forbids(self) -> None:
        decision = decide_training_use(TINHTE_ROBOTS)

        self.assertFalse(decision.allowed)
        self.assertIn("bytespider", decision.reason)
        self.assertIn("claudebot", decision.reason)

    def test_open_robots_allows(self) -> None:
        self.assertTrue(decide_training_use(OPEN_ROBOTS).allowed)
        self.assertTrue(decide_training_use("").allowed)

    def test_partial_ai_block_allows(self) -> None:
        self.assertTrue(decide_training_use(PARTIAL_AI_BLOCK).allowed)

    def test_grouped_user_agents_share_rules(self) -> None:
        robots = "User-agent: CCBot\nUser-agent: GPTBot\nDisallow: /\n"

        self.assertIn("ccbot", decide_training_use(robots).reason)
