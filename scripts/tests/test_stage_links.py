import importlib.util
from pathlib import Path
import unittest
from unittest.mock import patch, MagicMock

spec = importlib.util.spec_from_file_location("stage_links", Path(__file__).resolve().parents[1] / "sync-stage-links.py")
sync = importlib.util.module_from_spec(spec)
spec.loader.exec_module(sync)


class StageLinkTests(unittest.TestCase):
    def test_catalog_excludes_comments_and_deduplicates(self):
        source = '''private static readonly List<Stage> stages = [
            //new("Old", Tier.A, "old"),
            new("Battle", Tier.A, "battle"),
            new("Battle", Tier.A, "battle", 1),
            new("Battle", Tier.A, "battle", 2),
        ];'''
        self.assertEqual(sync.local_stages(source), [("Battle", 1), ("Battle", 2)])

    def test_punctuation_matches_but_sequels_remain_distinct(self):
        self.assertEqual(sync.normalize("Battle: RE [Movie Edition]"), sync.normalize("Battle RE\n[Movie Edition]"))
        self.assertNotEqual(sync.normalize("Battle"), sync.normalize("Battle 2"))

    def test_directory_decodes_entities(self):
        self.assertEqual(sync.parse_directory('<events v-bind:eventjson="[{&quot;id&quot;:7,&quot;name&quot;:&quot;A &amp; B&quot;}]"></events>')[0]["name"], "A & B")

    def test_stage_parser_uses_visible_levels_and_rejects_other_hosts(self):
        source = '''<div>Level 7: Battle</div>
        <a href="https://dokkaninfo.com/events/challenge/701/7010075">SUPER</a>
        <a href="https://other.example/events/challenge/701/7010075">Fake</a>
        <div>Level 8: Battle</div><a href="/events/challenge/701/7010085">SUPER</a>'''
        stages = sync.parse_stages(source, 701)
        self.assertEqual(stages[7], {"https://dokkaninfo.com/events/challenge/701/7010075"})
        self.assertEqual(stages[8], {"https://dokkaninfo.com/events/challenge/701/7010085"})

    def test_bad_responses_fail_instead_of_creating_empty_output(self):
        for parse in [sync.parse_directory, lambda text: sync.parse_stages(text, 701)]:
            with self.assertRaises(ValueError):
                parse("<html>Service unavailable</html>")

    def test_ambiguous_event_requires_explicit_override(self):
        events = [{"id": 1, "name": "Battle"}, {"id": 2, "name": "Battle"}]
        details = {1: {1: {"first"}}, 2: {1: {"second"}}}
        links, issues = sync.build_links([("Battle", 1)], events, details)
        self.assertFalse(links)
        self.assertEqual(len(issues), 1)
        links, issues = sync.build_links([("Battle", 1)], events, details, {"Battle": 2})
        self.assertEqual(links["Battle, Stage 1"], "second")
        self.assertFalse(issues)

    def test_multiple_difficulties_link_to_choices_not_arbitrary_difficulty(self):
        links, issues = sync.build_links([("Battle", 1), ("Battle", 2)], [{"id": 701, "name": "Battle"}], {701: {1: {"easy", "hard"}}})
        self.assertEqual(links["Battle, Stage 1"], "https://dokkaninfo.com/events/challenge/701")
        self.assertEqual(len(issues), 1)

    def test_failed_fetch_preserves_existing_catalog(self):
        target = MagicMock()
        target.exists.return_value = True
        target.read_text.return_value = '{"Battle, Stage 1": "https://dokkaninfo.com/events/challenge/701"}'
        catalog = 'private static readonly List<Stage> stages = [\n new("Battle", Tier.A, "battle"),\n];'
        directory_html = '<events v-bind:eventjson="[{&quot;id&quot;:701,&quot;name&quot;:&quot;Battle&quot;}]"></events>'
        for result in [RuntimeError("network unavailable"), '<div>Level 2: New stage</div><a href="/events/challenge/701/7010025">SUPER</a>']:
            with patch.object(sync, "TARGET", target), patch.object(Path, "read_text", side_effect=[catalog, '{}']), patch.object(sync, "fetch", side_effect=[directory_html, result]), patch("sys.argv", ["sync-stage-links.py"]):
                with self.assertRaises((RuntimeError, ValueError)):
                    sync.main()
            target.write_text.assert_not_called()
            target.with_suffix.assert_not_called()
