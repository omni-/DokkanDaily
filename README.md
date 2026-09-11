# DokkanDaily
*Daily challenges for the hit mobile game Dragon Ball Z: Dokkan Battle.*

[>> SITE LINK <<](https://dokkandle.net/daily)

### Coming Soon(tm)
* ~~Announcement banner~~
* ~~Full support of JP clears~~
* ~~Seasonal leaderboard~~
* Festival of Battles Day

### Feedback/Suggestions
Please submit an [Issue](https://github.com/omni-/DokkanDaily/issues) if you have feedback or suggestions!

Or, if you're more technically inclined, [Submit a Pull Request](https://github.com/omni-/DokkanDaily/pulls) *(no promises on merging!)*

### Nerdy stuff
__Project TODO List:__
* Equivalency bins
* Allow merging of username pairs (details in [`ClearInsert.sql`](https://github.com/omni-/DokkanDaily/blob/master/src/DokkanDailyDB/Core/Stored%20Procedures/ClearInsert.sql))
* Support multiple webhook destinations
* ~~Set up modified scraper as an azure job/Script updating character json (github actions?)~~

### Stage/Event hyperlinks

After adding or renaming stages, run `python scripts/sync-stage-links.py` (Python 3.10+ and curl).
The monthly `refresh-character-data` workflow runs this too and includes link changes in its PR.
Use `--dry-run` to review matches without writing, or `--summary <file>` for a Markdown report.

The script matches normalized event names, reads numbered stage links from Dokkan Info, and
writes `wwwroot/data/DokkanStageLinks.json`. The app embeds this catalog at build time: visitors
make no extra requests. Stages with several difficulties link to the event's difficulty choices.
Network/parser failures or loss of an existing active match leave the previous catalog intact.
New unmatched/ambiguous stages appear in the report and render without a link; the data validation
suite flags missing links before merge. Rebuild/restart after regenerating the catalog.

`scripts/stage-link-overrides.json` holds only reviewed name exceptions:
- Global Campaign! Special Battle 2025 -> 1722: now named Special Battle 2025; its missions still use the old campaign name.
- Collection of Epic Battles -> 1769: the renewed two-stage Saiyan/Planet Namek event used by our current catalog, rather than original event 760.

Run parser/matching tests with `python -m unittest discover -s scripts/tests -p "test_*.py"`.

See [screenshot stage validation](docs/ocr-stage-validation.md) for conservative upload behavior, runtime verification and research provenance.
