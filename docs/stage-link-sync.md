# Stage links and OCR alias synchronization

After adding or renaming stages, export metadata from the sibling scraper (Node and curl),
then import it in Daily (Python 3.10+). Keep intermediate JSON outside both repositories:

```bash
# From DokkanWebScraper
npm run run:dokkaninfo -- stage-metadata --out <scratch>/stage-metadata.json
# From DokkanDaily
python scripts/sync-stage-links.py --metadata <scratch>/stage-metadata.json
```
The monthly `refresh-character-data` workflow runs this too and includes link and OCR alias changes in its PR.
Use `--dry-run` to review matches without writing, or `--summary <file>` for a Markdown report.
Temporary replacement files stay in the system temp directory; it must share a filesystem with
the catalogs (set `TMPDIR`/`TEMP` to an external directory on that filesystem if needed).

The importer matches normalized event names against the scraper export and
writes `wwwroot/data/DokkanStageLinks.json` and `Ocr/StageTitleAliases.json`. DokkanWebScraper owns source acquisition and parsing of visible stage numbers, titles and all difficulty destinations. Daily owns matching, overrides, application link selection and alias preservation.
Scraped English alias pairs are refreshed while reviewed localizations and historical contrasts are retained.
The app embeds these catalogs at build time: visitors
make no extra requests. Stages with several difficulties link to the event's difficulty choices.
Network/parser failures, ambiguous names, or any missing configured stage stop the sync before
either catalog is written. The data validation suite also requires alias coverage for every configured
stage. Missing runtime data still permits uploads. Rebuild/restart after regenerating the catalogs.

`scripts/stage-link-overrides.json` holds only reviewed name exceptions:
- Global Campaign! Special Battle 2025 -> 1722: now named Special Battle 2025; its missions still use the old campaign name.
- Collection of Epic Battles -> 1769: the renewed two-stage Saiyan/Planet Namek event used by our current catalog, rather than original event 760.

Run importer/matching tests with `python -m unittest discover -s scripts/tests -p "test_*.py"`.

See [screenshot stage validation](ocr-stage-validation.md) for conservative upload behavior, runtime verification and research provenance.
