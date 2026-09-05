#!/usr/bin/env python3
"""Sync Dokkan Info links: python scripts/sync-stage-links.py [--dry-run] [--summary FILE].
Requires Python 3.10+ and curl. No browser or third-party Python packages required.
Only unique normalized event names and unique stage destinations are accepted.
"""
import argparse
from collections import defaultdict
from concurrent.futures import ThreadPoolExecutor
from html import unescape
from html.parser import HTMLParser
import json
from pathlib import Path
import re
import subprocess
import unicodedata

ROOT = Path(__file__).resolve().parents[1]
TARGET = ROOT / "src/DokkanDaily/wwwroot/data/DokkanStageLinks.json"
BASE = "https://dokkaninfo.com"


def normalize(value):
    return "".join(c for c in unicodedata.normalize("NFKC", value).casefold() if c.isalnum())


def local_stages(source):
    block = re.search(r"private static readonly List<Stage> stages\s*=\s*\[(.*?)\];", source, re.S)
    if not block:
        raise ValueError("Cannot find the Stage catalog; no links were changed.")
    rows = re.findall(r'^\s*new\("([^"\n]+)", Tier\.\w+, "([^"\n]+)"(?:, (\d+))?\)', block[1], re.M)
    if not rows:
        raise ValueError("Stage catalog is empty; no links were changed.")
    return list(dict.fromkeys((name, int(number or 1)) for name, _, number in rows))


def fetch(path):
    # Same transport as DokkanWebScraper's Dokkan Info adapter.
    result = subprocess.run(["curl", "--silent", "--show-error", "--fail", "--location",
                             "--compressed", "--max-time", "45", "--retry", "2",
                             BASE + path], capture_output=True, check=True)
    return result.stdout.decode("utf-8")


def parse_directory(source):
    match = re.search(r'v-bind:eventjson="([^"]+)"', source)
    if not match:
        raise ValueError("Dokkan Info event directory was not recognized.")
    events = json.loads(unescape(match[1]))
    if not isinstance(events, list) or not events:
        raise ValueError("Dokkan Info returned an empty event directory.")
    for event in events:
        if not isinstance(event.get("id"), int) or event["id"] <= 0 or not event.get("name"):
            raise ValueError("Invalid event in Dokkan Info directory.")
    return events


class StageParser(HTMLParser):
    def __init__(self, event_id):
        super().__init__(convert_charrefs=True)
        self.prefix = f"/events/challenge/{event_id}/"
        self.level = None
        self.links = defaultdict(set)

    def handle_data(self, data):
        match = re.match(r"^\s*Level (\d+):", data)
        if match:
            self.level = int(match[1])

    def handle_starttag(self, tag, attrs):
        href = dict(attrs).get("href", "").removeprefix(BASE)
        if tag == "a" and self.level is not None and re.fullmatch(re.escape(self.prefix) + r"\d+", href):
            self.links[self.level].add(BASE + href)


def parse_stages(source, event_id):
    parser = StageParser(event_id)
    parser.feed(source)
    if not parser.links:
        raise ValueError(f"No numbered stages found for event {event_id}; no links were changed.")
    return parser.links


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


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--summary", type=Path)
    args = parser.parse_args()
    stages = local_stages((ROOT / "src/DokkanDaily/Constants/DokkanConstants.cs").read_text(encoding="utf-8-sig"))
    events = parse_directory(fetch("/events/challenge"))
    overrides = json.loads((ROOT / "scripts/stage-link-overrides.json").read_text(encoding="utf-8"))
    names = {normalize(name) for name, _ in stages}
    needed = [event for event in events if normalize(event["name"]) in names or event["id"] in overrides.values()]
    def load(event):
        print(f"Reading {event['id']}: {event['name'].strip()}", flush=True)
        return event["id"], parse_stages(fetch(f"/events/challenge/{event['id']}"), event["id"])
    # Finish every fetch before touching the output; any network/parser error preserves it.
    with ThreadPoolExecutor(max_workers=3) as pool:
        details = dict(pool.map(load, needed))
    links, unresolved = build_links(stages, events, details, overrides)
    if not links:
        raise ValueError("No stages matched; existing links were preserved.")
    previous = json.loads(TARGET.read_text(encoding="utf-8")) if TARGET.exists() else {}
    active = {f"{name}, Stage {number}" for name, number in stages}
    lost = (previous.keys() & active) - links.keys()
    if lost:
        raise ValueError("Previously linked stages no longer match; existing links were preserved: " + "; ".join(sorted(lost)))
    report = f"## Stage link sync\n\nMatched {len(links)}/{len(stages)} stages.\n"
    if unresolved:
        report += "\nNeeds review (left unlinked):\n" + "".join(f"- {item}\n" for item in unresolved)
    if args.summary:
        args.summary.write_text(report, encoding="utf-8")
    print(report)
    if not args.dry_run:
        temp = TARGET.with_suffix(".json.tmp")
        temp.write_text(json.dumps(links, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        temp.replace(TARGET)


if __name__ == "__main__":
    main()
