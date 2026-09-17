#!/usr/bin/env python3
"""Import scraper metadata: python scripts/sync-stage-links.py --metadata FILE [--dry-run] [--summary FILE].
Requires Python 3.10+. No network or third-party Python packages required.
Ambiguous event matches fail; stages with multiple difficulties link to the event choices.
"""
import argparse
import copy
from collections import defaultdict
from contextlib import ExitStack
import json
import os
from pathlib import Path
import re
import tempfile
from types import SimpleNamespace
import unicodedata

ROOT = Path(__file__).resolve().parents[1]
TARGET = ROOT / "src/DokkanDaily/wwwroot/data/DokkanStageLinks.json"
ALIASES = ROOT / "src/DokkanDaily/Ocr/StageTitleAliases.json"
BASE = "https://dokkaninfo.com"
SCRAPED = "Dokkan Info stage sync: "


def normalize(value):
    return "".join(c for c in unicodedata.normalize("NFKC", value).casefold() if c.isalnum())


def local_stages(source):
    block = re.search(r"private static readonly List<Stage> stages\s*=\s*\[(.*?)\];", source, re.S)
    if not block:
        raise ValueError("Cannot find the Stage catalog; no links were changed.")
    rows = re.findall(r'^\s*new\("([^"\n]+)", Tier\.\w+, "([^"\n]+)"(?:, (\d+))?(?:, (?:minimumDifficulty:\s*)?StageDifficulty\.\w+)?\)', block[1], re.M)
    if len(rows) != len(re.findall(r'^\s*new\s*\(', block[1], re.M)):
        raise ValueError("Unsupported Stage declaration; catalogs were preserved.")
    if not rows:
        raise ValueError("Stage catalog is empty; no links were changed.")
    return list(dict.fromkeys((name, int(number or 1)) for name, _, number in rows))


def read_metadata(source):
    """Validate the versioned scraper boundary before applying any Daily policy."""
    data = json.loads(source)
    if not isinstance(data, dict) or type(data.get("schemaVersion")) is not int or data["schemaVersion"] != 1:
        raise ValueError("Unsupported stage metadata schema.")
    events = data.get("events")
    if not isinstance(events, list) or not events:
        raise ValueError("Empty event metadata.")
    directory, details = [], {}
    for event in events:
        if not isinstance(event, dict):
            raise ValueError("Invalid event metadata.")
        event_id, title = event.get("id"), event.get("title")
        if type(event_id) is not int or event_id <= 0 or event_id in details or not isinstance(title, str) or not title.strip():
            raise ValueError("Invalid or duplicate event metadata.")
        if event.get("sourceUrl") != f"{BASE}/events/challenge/{event_id}":
            raise ValueError("Invalid event source URL.")
        stages = event.get("stages")
        if not isinstance(stages, list) or not stages:
            raise ValueError("Empty event stages.")
        links, titles, ids = {}, {}, set()
        for stage in stages:
            if not isinstance(stage, dict):
                raise ValueError("Invalid stage metadata.")
            number, name = stage.get("number"), stage.get("title")
            if type(number) is not int or number <= 0 or number in links or not isinstance(name, str) or not name.strip():
                raise ValueError("Invalid, duplicate or conflicting stage title/number.")
            destinations = stage.get("destinations")
            if not isinstance(destinations, list) or not destinations:
                raise ValueError("Missing stage destinations.")
            urls = set()
            for destination in destinations:
                if not isinstance(destination, dict):
                    raise ValueError("Invalid stage destination.")
                dest_id = destination.get("id")
                if type(dest_id) is not int or dest_id <= 0 or dest_id in ids:
                    raise ValueError("Invalid or duplicate destination ID.")
                url = f"{BASE}/events/challenge/{event_id}/{dest_id}"
                if destination.get("url") != url:
                    raise ValueError("Invalid stage destination URL.")
                ids.add(dest_id)
                urls.add(url)
            links[number], titles[number] = urls, {" ".join(name.split())}
        directory.append(dict(id=event_id, name=" ".join(title.split())))
        details[event_id] = SimpleNamespace(links=links, titles=titles)
    return directory, details


def build_aliases(links, events, details, previous):
    """Refresh sourced English names, retaining reviewed localizations and historical contrasts."""
    rows = copy.deepcopy(previous)
    targets = {(row["eventId"], row["stageNumber"]): row for row in rows}
    if len(targets) != len(rows):
        raise ValueError("Duplicate alias targets; catalogs were preserved.")
    event_names = {event["id"]: " ".join(event["name"].split()) for event in events}
    for full_name, url in links.items():
        event_id = int(re.search(r"/challenge/(\d+)", url)[1])
        number = int(re.search(r", Stage (\d+)$", full_name)[1])
        titles = details[event_id].titles.get(number, set())
        if len(titles) != 1:
            raise ValueError(f"{full_name}: expected one visible stage title, got {len(titles)}; catalogs were preserved.")
        row = targets.setdefault((event_id, number), dict(eventId=event_id, stageNumber=number, aliases=[]))
        # Only replace entries owned by this scraper, never reviewed Japanese or historical names.
        row["aliases"] = [a for a in row["aliases"] if not (a.get("provenance") or "").startswith(SCRAPED)]
        alias = dict(eventTitle=event_names[event_id], stageTitle=next(iter(titles)),
                     provenance=SCRAPED + f"{BASE}/events/challenge/{event_id}")
        if not any(normalize(a["eventTitle"]) == normalize(alias["eventTitle"])
                   and normalize(a["stageTitle"]) == normalize(alias["stageTitle"]) for a in row["aliases"]):
            row["aliases"].append(alias)
    return [targets[key] for key in sorted(targets)]


def build_links(stages, events, details, overrides=None):
    by_name = defaultdict(list)
    for event in events:
        by_name[normalize(event["name"])].append(event)
    result, unresolved = {}, []
    for name, number in stages:
        candidates = by_name[normalize(name)]
        if name in (overrides or {}):
            candidates = [e for e in events if e["id"] == overrides[name]]
        if len(candidates) != 1:
            unresolved.append(f"{name}, Stage {number}: {len(candidates)} matching events")
            continue
        event = candidates[0]
        urls = details[event["id"]].get(number, set())
        if len(urls) == 1:
            result[f"{name}, Stage {number}"] = next(iter(urls))
        elif len(urls) > 1:
            # The app does not store difficulty. Link to the event's difficulty choices,
            # rather than silently choosing a difficulty the player wasn't assigned.
            result[f"{name}, Stage {number}"] = f"{BASE}/events/challenge/{event['id']}"
        else:
            unresolved.append(f"{name}, Stage {number}: numbered stage not found")
    return dict(sorted(result.items())), unresolved


def replace_catalogs(links, aliases):
    # Stage both files outside the repos before replacing either. If the second replacement
    # fails, restore the first. Temp storage must be on the catalogs' filesystem for rename.
    with ExitStack() as cleanup:
        def temporary(suffix):
            descriptor, name = tempfile.mkstemp(prefix="dokkan-stage-sync-", suffix=suffix)
            os.close(descriptor)
            path = Path(name)
            cleanup.callback(path.unlink, missing_ok=True)
            return path

        prepared = []
        for index, (target, data) in enumerate([(TARGET, links), (ALIASES, aliases)]):
            staged = temporary(f"-{index}.json")
            staged.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
            backup = temporary(f"-{index}.backup")
            existed = target.exists()
            if existed:
                backup.write_bytes(target.read_bytes())
            prepared.append((target, staged, backup, existed))
        replaced = []
        try:
            for target, staged, backup, existed in prepared:
                staged.replace(target)
                replaced.append((target, backup, existed))
        except OSError:
            for target, backup, existed in reversed(replaced):
                if existed:
                    backup.replace(target)
                else:
                    target.unlink()
            raise


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--metadata", type=Path, required=True, help="DokkanWebScraper stage-metadata JSON export")
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--summary", type=Path)
    args = parser.parse_args()
    stages = local_stages((ROOT / "src/DokkanDaily/Constants/DokkanConstants.cs").read_text(encoding="utf-8-sig"))
    events, details = read_metadata(args.metadata.read_text(encoding="utf-8"))
    overrides = json.loads((ROOT / "scripts/stage-link-overrides.json").read_text(encoding="utf-8"))
    links, unresolved = build_links(stages, events, {key: value.links for key, value in details.items()}, overrides)
    if not links:
        raise ValueError("No stages matched; existing links were preserved.")
    previous = json.loads(TARGET.read_text(encoding="utf-8")) if TARGET.exists() else {}
    active = {f"{name}, Stage {number}" for name, number in stages}
    lost = (previous.keys() & active) - links.keys()
    if lost:
        raise ValueError("Previously linked stages no longer match; existing links were preserved: " + "; ".join(sorted(lost)))
    if unresolved:
        raise ValueError("Incomplete stage coverage; catalogs were preserved: " + "; ".join(unresolved))
    previous_aliases = json.loads(ALIASES.read_text(encoding="utf-8"))
    aliases = build_aliases(links, events, details, previous_aliases)
    report = f"## Stage link and alias sync\n\nMatched {len(links)}/{len(stages)} stages with sourced English title pairs.\n"
    if args.summary:
        args.summary.write_text(report, encoding="utf-8")
    print(report)
    if not args.dry_run:
        replace_catalogs(links, aliases)


if __name__ == "__main__":
    main()
