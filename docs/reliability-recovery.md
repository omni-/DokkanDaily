# Deferred reliability fixes

Based on refreshed `origin/master` (`0aad793`), with `codex/deferred-pr-work` used only as reference.

## Recovered behavior

- Challenge calculation, seed changes, reset, tomorrow generation, and admin overrides share a
  semaphore in the singleton RNG service. Published challenges are replaced rather than mutated.
  Admin edits based on a superseded snapshot return a retry error. Expiry refreshes the UTC seed
  at midnight; seed and date are captured together and state is published after calculation succeeds.
- Leaders without matching character data are excluded from every selection and fallback pool.
  Complete loss of eligible leaders is reported explicitly rather than publishing a broken challenge.
  Exhausted repeat windows relax only the necessary stage/event exclusions. Target selection first
  uses the filtered tier pool, then the eligible full tier pool, then a random closest-tier target.
  Events remain uniformly selected, valid daily types remain equally likely, and exact-tier targets
  retain twice the weight of adjacent tiers. Unknown history types/names do not discard other history;
  exact names take precedence over unambiguous legacy truncated prefixes.
- Daily event/leader columns and procedure parameters support 150/200 characters. Date-normalized
  writes update an existing date under transaction/range locks, allowing a rerun to correct the row.
  The repository persists the challenge's own date. Migration retains the newest challenge row per
  calendar date and installs a temporary unique index in the cleanup transaction, closing the gap
  before the schema plan adds its permanent constraint. Post-deployment removes the temporary index.
  The existing StageClear migration and SQL application locks are preserved.
- Authentication keys use Microsoft's Azure Blob Data Protection provider with a stable application
  name and a private key container. Configured failures stop startup; no silent local fallback.
- OCR region caching uses a concurrent dictionary. Missing required regions produce an OCR error,
  including missing stage-details regions. File/HTTP/JSON resources are disposed. Upload extensions
  are case insensitive and the asynchronous file-view handler returns Task.

## Intentionally excluded

No deferred upload replacement/deletion behavior, container pruning, deployment changes, scoring
changes, reset-barrier changes, upload-admission changes, screenshot-validation changes,
leaderboard-deduplication changes, or old UI were brought back. Earlier submissions can still
contribute itemless points. No changes were needed to current GHCR deployment or redesigned UI.
The old branch's silent authentication-key fallback and migration that replaced StageClear cleanup
were rejected. Its unawaited RNG assertions were repaired rather than trusted as coverage.

Generation coordination is process-local, as is the existing challenge cache. Durable shared keys
support authentication across replicas; they do not synchronize admin overrides or make challenge
publication distributed. That would require a separate persistence/ownership design.

## Validation

Use the normal test project and SQL database build from CI. Database tests target only a disposable
SQL Server at `127.0.0.1:1433`, database `mydatabase`, with the repository's mock SA credentials.
For blob integration tests set `DOKKAN_TEST_BLOB_CONNECTION=UseDevelopmentStorage=true` and run
Azurite on localhost port 10000. Without that explicit setting, the blob integration test is skipped.
Each key test creates and deletes its own uniquely named container.

Behavioral coverage includes concurrent cache misses, generation/override ordering, stale admin
edits, midnight seed refresh, stale history, eligible leader fallback, concurrent region loads,
missing required regions, concurrent idempotent SQL writes, long names, challenge dates, private
key storage, cross-provider key reuse, and provider recreation. Existing OCR, reset, admission,
scoring and leaderboard tests remain part of the suite.

Docker Desktop verification uses a local production-mode site image, SQL Server and Azurite, dummy
OAuth settings, and a loopback-only webhook destination. No real Discord sign-in or production
services are used. Browser testing checks daily/clears/leaderboard rendering and submits an uppercase
PNG fixture: extension acceptance proceeds to OCR, where the wrong-event fixture is rejected.

The database migration is also exercised through SqlPackage against duplicate rows (including
same-day timestamps), with assertions that only the newest normalized row remains and the temporary
index is gone. Fresh deployment and rerunning the schema are checked separately.

## Results for this recovery

- Complete Release suite: 315 passed, zero failures or skips, with SQL and blob integration enabled.
- Application Release build/publish, production Docker build, and SSDT database build passed.
- Fresh installation, repeat installation, and upgrade from 100-character columns with duplicate
  DailyChallenge and StageClear rows passed. The combined upgrade retained itemless/high-score flags
  and the fastest historical time while widening the columns and collapsing challenge dates.
- SQL constraint failures rolled back and released their locks. Corrupt shared key XML failed the
  startup probe without replacing the stored ring. Shared providers and recreated providers could
  decrypt the original protected value.
- Browser checks passed for daily/clears/leaderboard, uppercase PNG submission and wrong-event
  rejection. Replacing the Docker app container reused the key ring without generating a new key.
- Final diff review found no remaining actionable regressions. The code preserves the protected
  upload/reset/scoring/deployment/UI paths described above. No deployment or merge was performed.

Existing tooling warnings remain: the OpenCV Ubuntu-specific runtime identifier warning, and the
locally installed SqlPackage's warning about SQL Server 2025 compatibility level 170. All relevant
builds and migration assertions passed despite those warnings. Real Discord OAuth and production
Azure credentials/permissions were not exercised; the mock uses local services and dummy credentials.
