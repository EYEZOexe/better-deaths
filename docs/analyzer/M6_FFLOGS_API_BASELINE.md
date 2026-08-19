# M6 FFLogs API baseline

Status date: 2026-08-19
Source of truth checked: current official FFLogs API/v2 GraphQL documentation.

This note records only the external API facts needed to keep M6 implementation work from relying on memory or deprecated v1 behavior. It is not a replacement for checking the live schema when request shapes are implemented.

## Authentication boundary

- FFLogs API v2 uses OAuth 2.0 and GraphQL.
- The public GraphQL endpoint is `/api/v2/client` and uses the OAuth client-credentials flow. It can access public information only.
- Private/user-authorized report access uses `/api/v2/user` and a user authorization flow (authorization-code or PKCE according to the current FFLogs documentation).
- Access tokens are sent as Bearer authorization values.
- The plugin must therefore treat public-client and user-authorized access as distinct source-auth modes while keeping all credential/token values inside the FFLogs integration boundary.

Official documentation checked:
- `https://www.fflogs.com/api/docs`
- `https://www.fflogs.com/v2-api-docs/ff/query.doc.html`

## Report/fight shape relevant to M6

Current v2 GraphQL schema documents:

- a report has a unique `code`, absolute report `startTime`/`endTime`, a `revision`, fights, master data, and paginated events;
- a `ReportFight` has an integer report fight `id`, `encounterID`, `name`, `startTime`, `endTime`, optional kill/in-progress information, and game-zone information;
- fight start/end times are millisecond timestamps relative to the beginning of the report;
- the report event query can be filtered to `fightIDs` and accepts time ranges and a configurable event `limit`;
- the current schema documents event limits from 100 through 10,000 per request;
- report event data is explicitly documented as non-frozen and may change, so cache design must account for report revision/source freshness instead of assuming an imported report can never change.

Official schema pages checked:
- `https://www.fflogs.com/v2-api-docs/ff/report.doc.html`
- `https://www.fflogs.com/v2-api-docs/ff/reportfight.doc.html`

## M6 implementation consequences

1. Do not use the deprecated v1 API for new integration work.
2. Do not put endpoint URLs, OAuth details, GraphQL DTOs, access tokens, or client secrets into Domain or Analysis contracts.
3. Keep report code/fight ID as source provenance rather than adding FFLogs-specific fields to `RecordedPull`.
4. Treat public and user-authorized access separately; a private report that is unavailable to the current auth mode is an integration error, not an analyzer failure.
5. Build pagination around the v2 event paginator and fixture-test page continuation before enabling real imports.
6. Key any persistent event/metadata cache by enough source identity/revision information to avoid silently reusing stale re-exported report data.
7. Re-check the current GraphQL schema before M6-B request/query strings are finalized.
