import importlib.util
from pathlib import Path
import unittest
import json
import tempfile
from types import SimpleNamespace
from unittest.mock import patch, MagicMock

spec = importlib.util.spec_from_file_location("stage_links", Path(__file__).resolve().parents[1] / "sync-stage-links.py")
sync = importlib.util.module_from_spec(spec)
spec.loader.exec_module(sync)


class StageLinkTests(unittest.TestCase):
    def test_catalog_includes_minimum_difficulties_and_rejects_unknown_syntax(self):
        source = '''private static readonly List<Stage> stages = [
            new("Battle", Tier.A, "battle", minimumDifficulty: StageDifficulty.SUPER3),
            new("Battle", Tier.A, "battle", 2, StageDifficulty.SUPER3),
        ];'''
        self.assertEqual(sync.local_stages(source), [("Battle", 1), ("Battle", 2)])
        with self.assertRaises(ValueError):
            sync.local_stages(source.replace('2, StageDifficulty.SUPER3', 'stage: 2'))

    def test_alias_sync_preserves_reviewed_names_and_refreshes_scraped_names(self):
        events = [{"id": 1769, "name": "Collection of Epic Battles"}]
        details = {1769: SimpleNamespace(titles={2: {"Planet Namek Saga"}})}
        links = {"Collection of Epic Battles, Stage 2": sync.BASE + "/events/challenge/1769/17690025"}
        reviewed = dict(eventTitle="日本語イベント", stageTitle="日本語ステージ", provenance="reviewed")
        previous = [dict(eventId=1769, stageNumber=2, aliases=[reviewed,
            dict(eventTitle="Old", stageTitle="Old", provenance=sync.SCRAPED + "old")]),
            dict(eventId=760, stageNumber=1, aliases=[reviewed])]
        result = sync.build_aliases(links, events, details, previous)
        aliases = result[1]["aliases"]
        self.assertEqual(result[0], previous[1])  # Historical event retained.
        self.assertEqual(aliases[0], reviewed)
        self.assertEqual(aliases[1]["stageTitle"], "Planet Namek Saga")
        self.assertEqual(aliases[1]["eventTitle"], "Collection of Epic Battles")
        self.assertEqual(result, sync.build_aliases(links, events, details, result))
        self.assertEqual(previous[0]["aliases"][1]["stageTitle"], "Old")  # Inputs untouched.

    def test_missing_or_conflicting_visible_names_fail_alias_sync(self):
        events = [{"id": 7, "name": "Battle"}]
        links = {"Battle, Stage 1": sync.BASE + "/events/challenge/7"}
        for titles in [set(), {"A", "B"}]:
            details = {7: SimpleNamespace(titles={1: titles})}
            with self.assertRaises(ValueError):
                sync.build_aliases(links, events, details, [])

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

    def test_metadata_boundary_rejects_malformed_or_conflicting_data(self):
        valid = {"schemaVersion": 1, "events": [{"id": 701, "title": "Battle", "sourceUrl": sync.BASE + "/events/challenge/701",
            "stages": [{"number": 7, "title": "Goku & Vegeta", "destinations": [{"id": 7010075, "url": sync.BASE + "/events/challenge/701/7010075"}]}]}]}
        events, details = sync.read_metadata(json.dumps(valid))
        self.assertEqual(events, [{"id": 701, "name": "Battle"}])
        self.assertEqual(details[701].titles[7], {"Goku & Vegeta"})
        import copy
        variants = [{}, {"schemaVersion": 2, "events": valid["events"]}, {"schemaVersion": 1, "events": []}]
        for path, value in [(('events', 0, 'stages'), []), (('events', 0, 'stages', 0, 'title'), ''),
                            (('events', 0, 'stages', 0, 'destinations', 0, 'url'), 'https://other.example/fake'),
                            (('events', 0, 'stages', 0, 'number'), True)]:
            bad = copy.deepcopy(valid)
            target = bad
            for key in path[:-1]: target = target[key]
            target[path[-1]] = value
            variants.append(bad)
        bad = copy.deepcopy(valid)
        bad['events'][0]['stages'].append(copy.deepcopy(bad['events'][0]['stages'][0]))
        variants.append(bad)
        for bad in variants:
            with self.assertRaises(ValueError):
                sync.read_metadata(json.dumps(bad))

    def test_failed_or_incomplete_import_preserves_both_catalogs(self):
        target, aliases = MagicMock(), MagicMock()
        target.exists.return_value = True
        target.read_text.return_value = '{"Battle, Stage 1": "https://dokkaninfo.com/events/challenge/701"}'
        catalog = 'private static readonly List<Stage> stages = [\n new("Battle", Tier.A, "battle"),\n];'
        incomplete = json.dumps({"schemaVersion": 1, "events": [{"id": 701, "title": "Battle",
            "sourceUrl": sync.BASE + "/events/challenge/701", "stages": [{"number": 2, "title": "New",
            "destinations": [{"id": 7010025, "url": sync.BASE + "/events/challenge/701/7010025"}]}]}]})
        for metadata in ['{}', incomplete]:
            with patch.object(sync, "TARGET", target), patch.object(sync, "ALIASES", aliases), patch.object(Path, "read_text", side_effect=[catalog, metadata, '{}']), patch("sys.argv", ["sync-stage-links.py", "--metadata", "input.json"]):
                with self.assertRaises(ValueError):
                    sync.main()
            for output in [target, aliases]:
                output.write_text.assert_not_called()
                output.with_suffix.assert_not_called()

    def test_dry_run_validates_aliases_without_writing(self):
        target, aliases = MagicMock(), MagicMock()
        target.exists.return_value = True
        target.read_text.return_value = '{}'
        aliases.read_text.return_value = '[]'
        catalog = 'private static readonly List<Stage> stages = [\n new("Battle", Tier.A, "battle"),\n];'
        metadata = json.dumps({"schemaVersion": 1, "events": [{"id": 701, "title": "Battle",
            "sourceUrl": sync.BASE + "/events/challenge/701", "stages": [{"number": 1, "title": "New",
            "destinations": [{"id": 7010015, "url": sync.BASE + "/events/challenge/701/7010015"}]}]}]})
        with patch.object(sync, "TARGET", target), patch.object(sync, "ALIASES", aliases), patch.object(Path, "read_text", side_effect=[catalog, metadata, '{}']), patch("sys.argv", ["sync-stage-links.py", "--metadata", "input.json", "--dry-run"]):
            sync.main()
        target.with_suffix.assert_not_called()
        aliases.with_suffix.assert_not_called()

    def test_catalog_replacement_rolls_back_on_second_failure(self):
        with tempfile.TemporaryDirectory() as scratch:
            target, aliases = Path(scratch) / "links.json", Path(scratch) / "aliases.json"
            target.write_text("old links")
            aliases.write_text("old aliases")
            original_replace = Path.replace
            def fail_second(path, destination):
                if path.name.endswith("-1.json"):
                    raise OSError("simulated replacement failure")
                return original_replace(path, destination)
            with patch.object(sync, "TARGET", target), patch.object(sync, "ALIASES", aliases), patch.object(Path, "replace", fail_second):
                with self.assertRaises(OSError):
                    sync.replace_catalogs({"new": "link"}, [])
            self.assertEqual(target.read_text(), "old links")
            self.assertEqual(aliases.read_text(), "old aliases")
            with patch.object(sync, "TARGET", target), patch.object(sync, "ALIASES", aliases):
                sync.replace_catalogs({"new": "link"}, [])
            self.assertEqual(json.loads(target.read_text()), {"new": "link"})
            self.assertEqual(json.loads(aliases.read_text()), [])
