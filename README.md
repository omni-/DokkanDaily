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
The monthly `refresh-character-data` workflow runs this too and includes link and OCR alias changes in its PR.
Use `--dry-run` to review matches without writing, or `--summary <file>` for a Markdown report.

The script matches normalized event names, reads numbered stage links from Dokkan Info, and
writes `wwwroot/data/DokkanStageLinks.json` and `Ocr/StageTitleAliases.json`. It reads the visible
`Level N: Title` headings using the same curl transport and numbered headings as DokkanWebScraper.
Scraped English alias pairs are refreshed while reviewed localizations and historical contrasts are retained.
The app embeds these catalogs at build time: visitors
make no extra requests. Stages with several difficulties link to the event's difficulty choices.
Network/parser failures, ambiguous names, or any missing configured stage stop the sync before
either catalog is written. The data validation suite also requires alias coverage for every configured
stage. Missing runtime data still permits uploads. Rebuild/restart after regenerating the catalogs.

`scripts/stage-link-overrides.json` holds only reviewed name exceptions:
- Global Campaign! Special Battle 2025 -> 1722: now named Special Battle 2025; its missions still use the old campaign name.
- Collection of Epic Battles -> 1769: the renewed two-stage Saiyan/Planet Namek event used by our current catalog, rather than original event 760.

Run parser/matching tests with `python -m unittest discover -s scripts/tests -p "test_*.py"`.

See [screenshot stage validation](docs/ocr-stage-validation.md) for conservative upload behavior, runtime verification and research provenance.

### Shared authentication keys

Data Protection keys are stored privately in `data-protection/keys.xml` using
`DokkanDailySettings:AzureBlobConnectionString`. Production and staging require this
setting; Development can use local keys only when the connection string is absent.
Configured storage failures stop startup rather than falling back to incompatible local keys.
The startup check also reads/protects/unprotects through the key ring before serving requests.

Optional settings (also supported with the existing `DOTNET_` prefix):
- `DokkanDailySettings:DataProtectionContainerName` (default `data-protection`)
- `DokkanDailySettings:DataProtectionApplicationName` (default `DokkanDaily`)

Keep both stable across replicas/restarts. Use separate containers for unrelated environments.
The connection needs container creation/access-policy-read and key blob read/write permissions.
Existing public containers are rejected. Exclude the key container from storage lifecycle deletion;
retain old keys so existing cookies and protected session data remain readable. Azure Storage
protects stored blobs at rest; this configuration does not add application-level XML key encryption.
Switching from the previous local ring can require a one-time sign-in; local keys are not imported.

See [reliability recovery notes](docs/reliability-recovery.md) for validation and behavior details.
