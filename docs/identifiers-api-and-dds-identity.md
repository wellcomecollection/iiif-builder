# The Identifiers API and the DDS identity service

How and when the DDS should use the catalogue's Identifiers API (RFC 089), how that service relates
to the DDS's own identity service (RFC 081), whether it can make our service more authoritative, and
how to get from any identifier to the preserved files in the storage service.

**Status:** design notes, 2026-10-07, revised 2026-10-20. The client (`IIdentifiersApi`) and the
dashboard page (`/dash/Identifiers`) exist; nothing in the DDS's own identity service calls the API yet.

**Read first:** [RFC 085](https://github.com/wellcomecollection/docs/blob/main/rfcs/085-IIIF-Identities-and-Migration/README.md)
is the key document for the DDS. [RFC 091](https://github.com/wellcomecollection/docs/blob/main/rfcs/091-digitisation-ingest-identifiers/README.md)
is the decision record for the preservation identifier, and [RFC 089](https://github.com/wellcomecollection/docs/blob/main/rfcs/089-identifiers-api/README.md)
is the API contract. [RFC 083](https://github.com/wellcomecollection/docs/blob/main/rfcs/083-stable_identifiers/README.md)
describes the registry the API reads, and [RFC 081](https://github.com/wellcomecollection/docs/blob/main/rfcs/081-identifiers-in-iiif-builder/README.md)
is the DDS identity service.

## Three places identity lives

| Holder | What it knows | What it cannot know |
| --- | --- | --- |
| **ID Registry**, via the Identifiers API | One canonical id per source tuple `(type, sourceSystem, value)`, written by the ID Minter when a work first flows through the pipeline. Predecessor aliases (RFC 083) share a canonical id. Includes the METS-sourced work's own identity, `mets/{record id}`. | Which canonical id is the public work. Merge is computed downstream from mutable metadata, so the registry holds every candidate and none of the outcome (RFC 089 open question 4). Anything below work level. |
| **Catalogue API** | The merged public work, its full `identifiers` list (including `mets` and `calm-ref-no` rows the registry minted separately), its IIIF digital locations. It **redirects** a non-public canonical id to the public work it merged into. | Anything the pipeline hasn't processed yet. Sub-package structure. |
| **DDS identity service** (`DdsIdentity` table, `PersistedIdentityService`) | The preservation identifier as a package identifier, with its true casing, generator, storage space, the current `CatalogueId`, and every volume and issue identifier beneath it. | Today, nothing it hasn't parsed from a string or been told by a workflow message, a storage probe, or a catalogue search. |

RFC 085 draws the line: the Identifiers API is about works; the DDS identity service is about digital
objects and their sub-structure, and for the Work ID to preservation identifier redirect binding the
DDS's stored record *is* the serving authority, with the catalogue API as its upstream.

## Vocabulary, with real values

Everything below is a live observation made on 2026-10-07 against `api.wellcomecollection.org`.

| Term | Digitised example | Born-digital example |
| --- | --- | --- |
| Workflow message `identifier` (Goobi / Archivematica) | `b14658197` (may lack its check digit) | `ARTCOO/B/15` (may arrive with underscores) |
| DDS package identifier (`DdsIdentity.PackageIdentifier`) | `b14658197` | `ARTCOO/B/15` (case preserved) |
| Preservation identifier (RFC 091): storage `externalIdentifier`, canonical Manifest URI | `b14658197` | `ARTCOO/B/15` |
| Registry `mets` row (METS record identifier, lowercased) | `mets/b14658197` → canonical `f22jwsw5` | `mets/artcoo/b/15` → canonical `q8fhz52m` |
| Registry row for the catalogue record | `sierra-system-number/b14658197` → `nydjbrr7` | `calm-record-id/{guid}` → `vaqg68j6` (alias `axiell-guid`) |
| Other registry rows with their own canonical ids | — | `calm-ref-no/ARTCOO/B/15` → `m2fe2wrv`, `sierra-system-number/b30175860` → `q5mnbdna` |
| Catalogue API behaviour | `/works/f22jwsw5` 302 → `/works/nydjbrr7` | `/works/q8fhz52m` and `/works/q5mnbdna` 302 → `/works/vaqg68j6`; `/works/m2fe2wrv` 404 |
| Public work's IIIF location carries the preservation identifier | `/presentation/v2/b14658197` | `/presentation/ARTCOO/B/15` |
| Public work's `identifiers` list | `sierra-system-number b14658197`, no `mets` entry at all | `mets artcoo/b/15`, `calm-ref-no ARTCOO/B/15`, `calm-altref-no ART/COO/B/15`, `sierra-system-number b30175860`, `axiell-guid …` |

Things to hold on to:

- **The `mets` row is the preservation identifier, lowercased.** The pipeline mints the METS work's
  identity from the METS record identifier, and today (and under the Folio HRID recommendation) that
  is the same string as the storage `externalIdentifier`. RFC 091 open question 2 is the only case
  where they would diverge. `IdentifierSet.PreservationIdentifier` in the client encodes this.
- **The METS work always has its own canonical id**, never the public work's (RFC 091, "METS works
  keep their own id"). So the registry can take you from a preservation identifier to *a* canonical id,
  but only the catalogue API's redirect takes you on to the public work.
- **From a public work, the IIIF location is the reliable way back to the preservation identifier.**
  A `mets` entry in the work's `identifiers` is present on some merged works (`vaqg68j6`, `a9jwm2jc`)
  and absent on others (`nydjbrr7`, which still has `/presentation/v2/b14658197` as its IIIF
  location). RFC 085 says it directly: "the catalogue API's digital locations carry the real
  Manifest URI, so links are followed rather than constructed". Read the location, not the
  identifier list.
- **Not every registry canonical id is a work.** `calm-ref-no/ARTCOO/B/15` has a canonical id because
  the Minter mints one for every identifier on a record, but it is not a work and the catalogue API
  404s on it. The resolve page should treat such hits as corroboration, not as a destination.
- **The `mets` value is lowercased, and that loses information the storage service needs.** The
  METS transformer lowercases the record identifier
  ([`MetsData.scala`](https://github.com/wellcomecollection/catalogue-pipeline/blob/main/pipeline/transformer/transformer_mets/src/main/scala/weco/pipeline/transformer/mets/transformer/MetsData.scala))
  because Goobi occasionally used to write b numbers as `B20442233` while Sierra has `b20442233`; the code comment
  assumes an Archivematica record identifier "will already be a lowercase UUID", which is not true
  for born-digital METS, where it is the CALM ref. So `ARTCOO/B/15` becomes `mets/artcoo/b/15` as
  collateral damage. The storage service is **case-sensitive**: `bags/born-digital/ARTCOO/B/15` is
  200 and `bags/born-digital/artcoo/b/15` is 404 (checked 2026-10-07). The Identifiers API lookup
  is case-insensitive, so the lowercased value still *finds* the row, but the value it returns
  cannot be used as a storage key on its own. The true casing has to come from somewhere else: the
  DDS identities table (keyed on the lowercased value precisely so that this works), or the
  catalogue work's `calm-ref-no` identifier, which keeps its case. The merge candidate, by contrast,
  is only lowercased on the Sierra branch; the CALM branch keeps the ref's case, so merging is
  unaffected. A new identifier scheme must be lowercase from birth (RFC 085), which the HRID is;
  that avoids the problem rather than fixing it for the born-digital estate. Worth raising with the
  pipeline team, though the registry row is immutable once minted, so a fix would only apply to
  new rows.
- **The workflow message identifier is the preservation identifier, before normalisation.** The DDS
  already normalises every message through the identity service and keys the job on the package
  identifier. Nothing in the RFCs changes the message (RFC 085, "The workflow message").

### Worked example: an archive item digitised under a b number

`b18304175` is the "historical overlap" class that RFC 085 and RFC 091 both call out: archive
material digitised through Goobi before the migration was given a Sierra b number, so it is preserved
at `digitised/b18304175` and served at `/presentation/b18304175`, but its public work is the CALM
record (RFC 092: digitised archive material merges into a CALM, later Axiell, target). The work is
[`a9jwm2jc`](https://wellcomecollection.org/works/a9jwm2jc), reference number `SA/MAC/J.3/32`.
Observed 2026-10-20:

| Source tuple | Registry canonical id | Catalogue API on that id |
| --- | --- | --- |
| `sierra-system-number` / `b18304175` | `er85sbxs` | 302 to `a9jwm2jc` |
| `mets` / `b18304175` | `af7bkze4` | 302 to `a9jwm2jc` |
| `calm-ref-no` / `SAMAC/J/3/32` | `rascg44w` | 404 |
| `calm-record-id` / GUID, alias `axiell-guid` | `a9jwm2jc` | 200, the public work |
| `calm-altref-no` / `SA/MAC/J.3/32` | not minted | |

The public work's `identifiers` carry every one of these, including `mets: b18304175`, and its
IIIF location is `/presentation/v2/b18304175`. The DDS already redirects `/presentation/SA/MAC/J.3/32`,
`/presentation/SAMAC/J/3/32` and `/presentation/a9jwm2jc` to `/presentation/b18304175`.

What it shows:

- **The registry cannot take you from the b number to the CALM ref, or back.** The four canonical
  ids are not linked to each other. The registry links source identifiers only through predecessor
  relationships, which say "this is the same record in a new system". The Sierra bib and the CALM
  record are two different records whose union is a merge, computed downstream from mutable
  metadata, and RFC 089 and 091 both say the registry does not hold merge outcomes. The only place
  `b18304175` and `SAMAC/J/3/32` meet is the merged work in the catalogue API, which is what the
  DDS's `GetWorkByOtherIdentifier` query already reads. This is the designed boundary, not a gap.
- **From the b number you can still reach the public work**, by following the catalogue API's
  redirect on either `er85sbxs` or `af7bkze4`. **From the CALM ref you cannot.** `calm-ref-no` is
  minted as a secondary identifier and `rascg44w` is not a work; the CALM record's own canonical id
  comes from `calm-record-id`. For CALM refs the registry is a dead end for reaching the work.
- **`calm-ref-no` and `calm-altref-no` are two CALM fields.** `RefNo` is the hierarchical reference
  code CALM uses for its tree, one path element per level (`SAMAC/J/3/32`). `AltRefNo` is the
  alternative reference shown to the public and used in citations (`SA/MAC/J.3/32`), which the
  catalogue API exposes as `referenceNumber`. Born-digital preservation identifiers use the `RefNo`
  form (`ARTCOO/B/15`, `SABTS/A/2/10`). In both objects probed so far the registry mints
  `calm-ref-no` and never `calm-altref-no`, consistent with `calm-ref-no` serving as a merge
  candidate while `AltRefNo` is only ever a secondary identifier. The exact mechanism is unconfirmed.
- **For the candidate generator**, two consequences. Nothing built on the registry could suggest
  `calm-ref-no` from a b number, so the resolve page not doing so is correct. And a
  `calm-altref-no` candidate would be dead weight on current evidence, so the generator does not
  try one (removed 2026-10-20); resolving an `AltRefNo` needs the catalogue API.
- **For "where are its files"** this object is the easy case: the b number is the preservation
  identifier and both its registry rows confirm it. The CALM forms only get there through the
  catalogue work's `mets` identifier.

## What exists now

- `Wellcome.Dds/Identifiers/`: `IIdentifiersApi`, the response models, `IdentifierCandidates`
  (shape-based candidate generation, which RFC 085 asks for by name), `IdentifierResolution`.
- `Wellcome.Dds.Repositories/Identifiers/WellcomeCollectionIdentifiersApi.cs`: the HTTP client.
  A 404 is `null`; anything else non-2xx throws `IdentifiersApiException` with the API's error body.
- `Wellcome.Dds/Identifiers/IIdentifierNavigator` and `Wellcome.Dds.Repositories/Identifiers/IdentifierNavigator.cs`:
  the helpers of "API helpers that navigate both services" below, composing the registry client, the
  catalogue client and a single-shot storage probe (`StorageServiceProbe`). `Navigate(id)` runs every
  route and keeps them separate so disagreements show. Registered in the dashboard and the workflow
  processor; nothing on the write path calls it yet.
- `/dash/Identifiers`: explicit forward and reverse lookups. `/dash/Identifiers/Resolve/{id}` runs
  `Navigate` and shows the DDS identity, the preservation identifier(s) with recovered casing, the
  public work by both routes with an agree/disagree flag, the storage probes (naming which storage
  service was asked), and every registry candidate tried. Linked from the Manifestation and Identity pages.
- Config section `IdentifiersApi` (`BaseUrl`, `ApiKey`). Only the dashboard registers the client;
  the server and the two processors do not yet. The dev key is in the git-ignored
  `appsettings.Development.json` and is valid on the production host. The staging key
  (`wellcome/identifiers_api/digirati/stage/api_key`) is valid only on
  `api-stage.wellcomecollection.org`; [iiif-builder-infrastructure #84](https://github.com/wellcomecollection/iiif-builder-infrastructure/pull/84)
  wires it, with a `BaseUrl` override, into all four staging services. Production has no key.

## How and when the DDS should use it

### Which process

| Process | Uses the API? | When |
| --- | --- | --- |
| Workflow processor | Yes, this is the main consumer | On the identity write path, once per workflow message (below) |
| Dashboard | Yes | The `/dash/Identifiers` pages, and the same write path when a workflow is started by hand |
| Server (iiif-builder) | No | Everything it needs is in the identities and manifestations tables by the time a request arrives |
| Job processor | No | It works from identities the workflow processor has already resolved |

The staging infrastructure gives all four the key so that this can change without another
infrastructure PR, but only the first two should ever call it.

### Not on the public request path

The API costs about 100ms a call and external keys are throttled at 10 requests a second with a burst
of 20 (RFC 089). `/presentation/{id}` must never call it synchronously. The DDS should be on the
internal usage plan when it becomes a real consumer, and even then every answer should be persisted in
the identities table and reused. RFC 085 says the same: lookups "persisted once per identifier".

### On the identity write path

`PersistedIdentityService.GetIdentity(s, generator)` takes the write path when a known generator is
supplied, which happens when a workflow message arrives. Today that path does three things:
validate the storage space by probing both spaces, set `CatalogueId` by a catalogue *search*
(`GetWorkByOtherIdentifier`, whose multiple-match rule is marked "NOT the real implementation yet"),
and a `ValidateSource` stub that always records `SourceValidated = false`.

This is where the Identifiers API belongs. Proposed additional step, after the storage probe:

1. `GetSiblingsBySourceIdentifier("mets", packageIdentifier)`. A hit proves the pipeline has
   registered a bag under this preservation identifier and gives the METS work's canonical id.
2. Follow the catalogue API redirect for that canonical id (`GetWorkByWorkId` already follows it,
   because `HttpClient` follows 302s) to get the public work. Its `Id` is `CatalogueId`.
3. A forward lookup of the public work's canonical id says which source system the *record* came
   from (`sierra-system-number`, `calm-record-id`, `folio-instance`, `axiell-guid`). That is what
   `ValidateSource` was waiting for, and it is the first authoritative value `Source` will ever have
   had. But `Source` must keep its current meaning, "the system that issued the package identifier",
   so `b18304175` stays `sierra` even though its public work is a CALM record. The registry's role is
   to *validate* that (the matching `sierra-system-number` / `calm-ref-no` / `folio-instance` row
   exists for the package identifier), not to supply a different value. See
   [What reads `Source`](#what-reads-source) for why.

Two things make this additive rather than a replacement:

- **Pipeline lag.** The DDS and the METS adapter both react to the same storage notification. On a
  first ingest the `mets` row may not exist yet when the DDS processes the message. The write path
  must treat a 404 as "not yet", keep the search-based `CatalogueId`, and try again on the next
  reprocess (known-generator messages always re-take the write path, so this happens naturally) or
  on a scheduled re-check of rows with `SourceValidated = false`.
- **Merged works aggregate several objects.** A public work can carry more than one `mets`
  identifier. `CatalogueId` is a many-to-one binding and that is fine; the reverse direction (work to
  preservation identifier) is one-to-many and belongs to the catalogue API, not to the DDS table.

### Choosing the work when the identifiers query returns several

`GetWorkByOtherIdentifier` queries `works?identifiers={value}` and, when more than one work comes
back, used to pick by a rule its own comment called "NOT the real implementation yet": the work whose
`sierra-identifier` is the short form of the requested b number, or whose work type is `k`
("unlikely to be a Miro item"). On 2026-10-20 every identifier probed, including the `b14658197`
the comment cites, returned exactly one work, so the rule may rarely fire now; the Miro-sharing
case it was written for appears to have been resolved by later merge rules. It still needs to be
right. The real rule, in order of strength (implemented 2026-10-28: steps 1, 2 and 4 in
`WorkSelection.Choose`, pure and unit-tested, applied by the catalogue client's
`FindWorksByOtherIdentifier`, which `GetWorkByOtherIdentifier` now reduces to; step 3 in
`IdentifierNavigator.LookupWorkByAnyIdentifier`, whose `WorkLookup` says which route answered):

1. **The work whose `iiif-presentation` location names the requested preservation identifier**
   (`/presentation/{id}` or `/presentation/v2/{id}`, case-insensitive). That location is the merge
   outcome saying "this digital object belongs to this work", which is exactly the binding
   `CatalogueId` records. `WorkExtensions.GetDigitisedBNumbers` already does this for b numbers; it
   needs the CALM-ref form too.
2. **Failing that, rank by how the identifier matched.** The query matches any identifier type, so a
   work can match through an incidental one. Prefer the work on which the requested value is its
   `mets`, then `sierra-system-number`, then `calm-ref-no`, then `calm-altref-no`, then anything
   else. This generalises the short-form rule and drops the `k` guess.
3. **Cross-check with the registry.** `by-source/mets/{id}`, then the catalogue redirect on the
   METS work's canonical id, gives the public work by a route that does not depend on search
   ranking. When both routes answer they should agree; on disagreement log loudly and prefer the
   redirect, which is the merger's own statement. When the registry has no row yet (pipeline lag),
   the search answer stands alone.
4. **If still ambiguous, return null and log** rather than throw, as the current code does. Callers
   already treat null as "no catalogue work", and a job without a `CatalogueId` is recovered on the
   next reprocess; an exception stops the workflow.

Keep refetching the chosen work by id with the full `include` list, as now, because the search
response is a cut-down work. The legitimately many-to-one cases need no rule: one work aggregating
several storage objects resolves correctly per preservation identifier, and one preservation
identifier on several works should not survive a merge, so if it appears it is lag or a pipeline
fault and null-and-log is the right response.

### What reads `Source`

`DdsIdentity.Source` is `sierra` or `calm`. Outside the identity service and display-only views
(the identity page, bulk analyse, storage ingest), seven places read it, and none of them asks what
the name says:

| Where | Test | What it actually wants to know |
| --- | --- | --- |
| `MetsRepository.GetAsync` | `Source == Sierra` | Is this Goobi METS with the b-number volume/issue structure, so walk `VolumePart` / `IssuePart`? |
| `Dlcs.GetImagesForIdentifier` | `Source != Sierra` throws | Can the DLCS be queried by a b-number-shaped string reference? |
| `DashController.ManifestationSearch` | `Source == Sierra` | Is the package identifier already path-safe, or is the underscore form needed? |
| `DashController.AssetType` | `Source == Sierra` | The same path-safety question |
| `PeekController.XmlView` | `Source == Sierra` | Can a manifestation id be derived by splitting the filename on `_`? |
| `BulkWorkflowModel` | counts `Source == Sierra` | How many of these are b numbers? (the text says "are from Sierra") |
| `PersistedIdentityService.ReadIdentity` | `Source == Calm` | Is this an identifier whose case cannot be normalised? |

In practice `Source == Sierra` means "this is a b number", standing in for three different
properties: Goobi METS structure, path-safety of the identifier, and case-normalisability. Nothing
reads it as "the catalogue system the public record came from". Two consequences:

- The registry must not *replace* `Source` with the public work's source system. For `b18304175`
  that would be `calm-record-id`, and `MetsRepository` would take the non-Sierra branch and break
  volume handling for every archive item digitised under a b number. If "which system the public
  record came from" is ever wanted, it is a new field next to `CatalogueId`, read from the public
  work's original registry row.
- These seven sites are the RFC 081 problem in miniature: they branch on `Source` when they mean
  `Generator`, or `IsBNumber`, or "has a volume suffix convention". An HRID package will be Goobi
  METS with volume structure and a path-safe, case-stable identifier, but it will not be `sierra`.
  Each site needs to ask its real question before the first such package arrives. That is HRID
  work, not registry work, but it was found here.

### For the redirect contract

`/presentation/{workId}` → `/presentation/{preservationId}` is served today from the Manifestations
table (`GetManifestationByAnyIdentifier` matches `WorkId`, `CalmRef` and `CalmAltRef`) rather than
from `DdsIdentity.CatalogueId`. RFC 085 commits the DDS to this redirect for all content and names the
stored record as the serving authority. The Identifiers API does not serve the redirect, but it makes
the stored `CatalogueId` trustworthy, which is what the redirect should eventually read from.

### After the migration

RFC 085 and 091 are explicit that shape-based inference must shrink to shape-based candidate
generation followed by lookup. `IdentifierCandidates` is that generator. The two new cases:

- **A Folio HRID arrives as a preservation identifier** (`in00012345`). The parser cannot classify it.
  The write path has the generator from the message `origin`, probes the storage space as now, and
  the registry answers `mets/in00012345` once the pipeline has run. The DDS needs the HRID shape
  added to `ParsingIdentityService` so it is not treated as a CALM ref (and volume suffixes split
  correctly).
- **Given a Folio id, which preservation identifier?** This is RFC 091's "Finding the predecessor":
  `GetSiblingsBySourceIdentifier("folio-instance", folioId)`. A `sierra-system-number` sibling is
  the legacy b number, which is the preservation identifier because that is how the object is named
  in storage, in Goobi's METS and in our public IIIF URIs. No sibling means the preservation
  identifier is the HRID. In both cases confirm with a `mets` lookup, because the registry does not
  link the public work's canonical id to the METS work's.

## Does it make our identity service more authoritative?

Per field of `DdsIdentity`:

| Field | Authoritative source today | What the Identifiers API adds |
| --- | --- | --- |
| `PackageIdentifier` and its casing | The workflow message (generator) | Nothing. The registry lowercases; the generator's casing stays ours. |
| `Generator` | Message `origin` on the write path; parsed shape otherwise | Nothing directly. Indirectly, the public work's source system tells you Goobi vs Archivematica is *not* inferable from the catalogue (an Axiell-catalogued item can be either). |
| `StorageSpace` | Storage service probe (`ValidateStorageSpace`) | Nothing. The registry does not know spaces. Keep probing. |
| `Source` | Nothing; always unvalidated | **Validated, not supplied.** It must keep meaning "the system that issued the package identifier" because every reader uses it as a proxy for "is a b number" (write path, step 3). The registry confirms the matching row exists. "The system the public record came from" would be a new field. |
| `CatalogueId` | Catalogue search by identifier, with heuristics for multiple matches | **Yes.** `mets` row → canonical id → catalogue redirect → public work. One lookup and one redirect, no search heuristics. The search stays as fallback for pipeline lag. |
| Volume and issue identity | METS enumeration (`RegisterAuthoritativeChild`) | Nothing, by design (RFC 085, "Sub-package identity"). |

So: more authoritative for `Source` and `CatalogueId`, unchanged for the rest. The registry is
authoritative for "which source identifiers share a canonical id"; it is not authoritative for where
files are, which work is public, or what a volume is.

## API helpers that navigate both services

These compose `IIdentifiersApi`, `ICatalogue` and `StorageServiceClient`. They belong in a small
service in `Wellcome.Dds.Repositories` that depends on all three, so the clients stay independent of
each other. The dashboard resolve page is the first consumer; the identity write path is the second.

| Helper | Does | Depends on |
| --- | --- | --- |
| `ResolvePreservationIdentifier(string any)` | Candidates for the string; returns the `mets` row(s) found, with the METS work canonical id. | Identifiers API |
| `GetPublicWork(string canonicalId)` | Follows the catalogue redirect; returns the public work and whether a redirect happened. | Catalogue API |
| `GetPreservationIdentifiersForWork(string workId)` | The preservation identifiers of every storage object merged into the work, read from its items' `iiif-presentation` locations (reliable), with `mets` identifiers as corroboration (present only on some works). The one-to-many direction. | Catalogue API |
| `FindWorkByAnyIdentifier(string any)` | The catalogue's `identifiers=` query, which the DDS already uses as `GetWorkByOtherIdentifier`, with the selection rule in [Choosing the work](#choosing-the-work-when-the-identifiers-query-returns-several). The only route for identifiers the registry never mints, such as a CALM `AltRefNo`. | Catalogue API, Identifiers API for the cross-check |
| `FindPredecessorPreservationIdentifier(string folioId)` | RFC 091's predecessor lookup, returning the b number or `null`. | Identifiers API |
| `LocatePreservedFiles(string any)` | The algorithm below. | All three |

## Given an identifier, where are its preserved files?

The storage service keys everything on `(space, externalIdentifier)` and nothing else. So the whole
question is: find the preservation identifier, then probe. The DDS already does the probing half.

1. **Normalise and split.** Run the string through the identity service. A b number gains its check
   digit, underscores become slashes for CALM refs, and a volume or issue suffix is stripped to give
   the package identifier. Anything the registry answers is at package level; it will never answer
   `b19974760_207`.
2. **If the DDS already has an authoritative row** (`FromGenerator` and `StorageSpaceValidated`), the
   answer is `StorageSpace` + `PackageIdentifier`. Stop.
3. **Find the preservation identifier.**
   - A b number or CALM ref *is* one. Skip to step 4.
   - A canonical id (8 chars, letters and digits 2 to 9, no o/i/l/1): forward lookup. If the set has
     a `mets` row, that is the preservation identifier. If it does not (it is a public work's id),
     fetch the work from the catalogue API and read the preservation identifier out of its items'
     `iiif-presentation` locations; there may be several, or none if nothing is digitised. Do not
     rely on the work's `mets` identifiers, which some merged works lack.
   - A source identifier (Folio UUID, Axiell GUID, CALM record id, HRID): reverse lookup with
     siblings to get the canonical id, then as above. For a Folio id prefer the predecessor rule:
     a `sierra-system-number` sibling is the preservation identifier.
   - Anything else: candidates, and take whichever `mets` row answers. If none does, fall back to
     the catalogue's `identifiers=` query (`GetWorkByOtherIdentifier`), which is the only route for
     identifiers the registry never mints, such as a CALM `AltRefNo` like `SA/MAC/J.3/32`, and then
     read the IIIF location as above.
4. **Recover the casing.** A preservation identifier that came from a `mets` row is lowercased and
   the storage service is case-sensitive. Look the lowercased value up in the DDS identities table
   (its key is the lowercased value, so this is a direct hit if the DDS has ever seen the object);
   failing that, take the `calm-ref-no` value from the public work's identifiers; failing that, the
   probe can only succeed for identifiers that are lowercase by nature (b numbers, HRIDs).
5. **Probe the storage service** with `LoadStorageManifest(space, preservationId)` for `digitised`
   then `born-digital`, as `ValidateStorageSpace` does. A hit returns the storage manifest.
6. **Read the locations.** `WellcomeBagAwareArchiveStorageMap.FromJObject` turns the manifest into
   the bucket (`location.bucket`) and, per file, a version-prefixed path; the S3 key is
   `{space}/{externalIdentifier}/{vN}/{path}`. The anchor METS file is at the root of that.

What a `mets` row does and does not tell you: it proves the pipeline registered a bag under that
identifier at some point, so the files existed; it does not say which space, and its absence does not
prove the files are missing (pipeline lag, or an object the pipeline ignores). The storage probe is
always the final word. The resolve page could show step 4's outcome inline, which would answer "if
they exist at all" on the page.

## Open points

- **Keys and throttles.** Staging is covered by iiif-builder-infrastructure #84 (stage host only).
  Production needs a key, on the internal plan, before anything but the dashboard uses the client.
- **`Source` readers branch on the wrong thing.** Seven call sites test `Source == Sierra` to mean
  Goobi structure, path-safety or case handling (see [What reads `Source`](#what-reads-source)).
  Each needs to ask its real question before an HRID package arrives. Not a registry concern, but
  found while settling what `Source` means.
- **Pipeline lag on first ingest** (above). Needs a re-check path for unvalidated rows.
- **The `mets` value is lowercased, but the storage service is case-sensitive.** Verified
  2026-10-07: `bags/born-digital/ARTCOO/B/15` is 200, `bags/born-digital/artcoo/b/15` and
  `Artcoo/B/15` are 404, `bags/digitised/B14658197` is 404. The registry's `mets` row for the same
  object is `artcoo/b/15`, because the METS transformer
  ([`MetsData.scala`](https://github.com/wellcomecollection/catalogue-pipeline/blob/main/pipeline/transformer/transformer_mets/src/main/scala/weco/pipeline/transformer/mets/transformer/MetsData.scala))
  lowercases the record identifier to reconcile Goobi's `B20442233` with Sierra's `b20442233`, on
  the stated assumption that an Archivematica record identifier "will already be a lowercase UUID".
  For born-digital METS the record identifier is the CALM ref, so that assumption fails and the ref
  is lowercased as collateral damage. Consequences:
  - The reverse lookup is case-insensitive on the value (`by-source/mets/ArTCoo%2Fb%2F15`,
    `ARTCOO%2FB%2F15` and `artcoo%2Fb%2F15` all return the same row), consistent with a
    case-insensitive MySQL collation on the `(OntologyType, SourceSystem, SourceId)` key. The
    response echoes the stored casing, not the requested one: the `mets` row comes back as
    `artcoo/b/15`, the `calm-ref-no` row as `ARTCOO/B/15`. Canonical ids are the exception: the
    gateway regex rejects upper case with a 400 before the Lambda is reached.
  - The registry can *identify* a preserved object from a `mets` row but cannot *locate* it: for
    any CALM ref the value is not a valid storage key. b numbers and HRIDs are lowercase by nature
    and unaffected.
  - The DDS identities table is the authority for casing. Its primary key is the lowercased value,
    so a `mets` value is a direct hit on it if the DDS has ever processed the object. The public
    work's `calm-ref-no` identifier is the fallback; it keeps its case because the CALM branch of
    `MetsMergeCandidate` does not lowercase.
  - Registry rows are immutable once minted. A transformer fix would apply only to rows minted
    afterwards; the existing born-digital `mets` rows stay lowercase whatever happens upstream.
  - Anything in the DDS that writes a `mets` value into a bag URL, an S3 key or a `/presentation/`
    URI must go through the casing-recovery step (step 4 of "where are its preserved files")
    first. The resolve page's Identity link does this today by resolving the DDS record.

  Related pipeline work: catalogue-pipeline #3789 and #3790 (platform#6773, October 2026) make the
  id minter resolve case variants of a source id to the stored canonical id, so a case change can no
  longer mint a second canonical id. They do not change what is stored: the registry records and
  returns whichever casing an adapter presented first. For born-digital METS works that is the
  transformer's lowercased form, so the recovery step above is still needed.

  Raised as [catalogue-pipeline #3796](https://github.com/wellcomecollection/catalogue-pipeline/issues/3796)
  (2026-10-20), proposing that the transformer lowercase only b-number-shaped record identifiers.
  Until it is fixed, and for every row minted before it is, treat
  `IdentifierSet.PreservationIdentifier` as an identity, never as a key.
- **`folio-instance-hrid` rows.** RFC 089 open question 2 says storing the HRID alongside the UUID is
  an optimisation the Minter *could* do. If it doesn't, the HRID is only reachable through Folio.
- **`calm-ref-no` canonical ids** are not works. The resolve page should label them.
- **Axiell ref stability** (RFC 091 open question 3) decides whether born-digital ever needs the
  predecessor path.
- **Sub-package candidate stems** (RFC 085): probe `in00012345` when `in00012345_0002` is not known.
  Not implemented.

## Plan

Agreed 2026-10-20. The order is deliberate: each step is safe to deploy on its own, and each later
step reuses the one before. Nothing here changes persisted data until step 3's flag is flipped.

### Step 1: the helpers, on the resolve page (built; deploying to staging)

`IIdentifierNavigator` / `IdentifierNavigator` / `StorageServiceProbe`, with `/dash/Identifiers/Resolve/{id}`
as the first consumer, and the client, probe and navigator registered in the workflow processor so
step 3 needs no wiring. Branch `feature/identifier-navigator`, on top of `feature/external-identity-service`
(PR #296). Unit tests: `IdentifierNavigatorTests`, `WellcomeCollectionIdentifiersApiTests`,
`IdentifierCandidatesTests`, `IdentifierSetTests`; live tests: `IdentifiersApiLiveTests`
(`Category=Manual`, env var `IDENTIFIERS_API_KEY`).

What staging can and cannot tell us:

- The Identifiers API on the stage host returned the same canonical ids as production for everything
  probed, so the registry side of the page is meaningful on staging.
- The staging DDS talks to the stage storage service, which holds only what has been ingested into
  staging. A storage miss on staging is not evidence that files are absent; the page names the storage
  API it asked. Objects that have been ingested into staging will probe correctly.
- Nothing on the page writes anything. It is safe to point Wellcome at it, and it lets them see the
  DDS as a consumer of the API in their own logs.

### Step 2: the real selection rule for the identifiers query (built)

`GetWorkByOtherIdentifier`'s multiple-match rule is replaced by the four-step rule in
[Choosing the work](#choosing-the-work-when-the-identifiers-query-returns-several). Steps 1, 2 and 4
(location, identifier-type ranking, null-and-log) are `WorkSelection` in `Wellcome.Dds/Catalogue`,
applied inside the catalogue client, so every existing caller of `GetWorkByOtherIdentifier` gets the
rule without change and the catalogue client still knows nothing about the registry. Step 3 (the
registry's `mets` row, then the catalogue redirect, preferred on disagreement) is
`IdentifierNavigator.LookupWorkByAnyIdentifier`, returning a `WorkLookup` that names the route and the
reason; `FindWorkByAnyIdentifier` reduces it to the work. The resolve page shows the selection reason
and the works the query returned but did not choose. `/dash/Identity/Stats` (added at the same time)
counts the identities table by level, source, storage space and generator, and shows how many package
rows a generator has asserted, how many are validated, how many are bound to a catalogue work, and the
recently processed packages for which no work was found; the figures to watch as steps 2 and 3 land. Branch `feature/identifier-selection-rule`, on top
of `feature/identifier-navigator`. Tests: `WorkSelectionTests` (the rule on hand-made works),
`IdentifierNavigatorTests` (the cross-check: registry absent, query ambiguous, agreement,
disagreement, registry errors). No data change: nothing on the write path calls the navigator yet;
that is step 3.

### Step 3: the registry on the identity write path, in shadow mode first

Behind a config flag (suggested `Dds:IdentifiersApiMode` = `off` | `shadow` | `write`), when a
workflow message takes the write path in `PersistedIdentityService`:

1. `GetSiblingsBySourceIdentifier("mets", packageIdentifier)`, then the catalogue redirect on the
   METS work's canonical id, giving the registry-derived public work.
2. Compare with the search-derived `CatalogueId` and with the stored `Source` (which the registry
   validates, never replaces: see [What reads `Source`](#what-reads-source)).
3. In `shadow` mode, log agreement, disagreement and registry 404s; write nothing. In `write` mode,
   set `CatalogueId` from the redirect when the registry answered, keep the search fallback when it
   did not (pipeline lag), and set `SourceValidated`.

Deploy `shadow` to staging. Staging subscribes to the real Goobi and born-digital topics, so it
processes every new ingest, which is the only traffic that can show anything the probes have not.
After a few weeks of logs with no unexplained disagreements, flip to `write` on staging, then
production once a production key exists. The storage probe is out of scope for the comparison on
staging, for the reason in step 1.

### Later, when there is data to test against

- Add the HRID shape to `ParsingIdentityService` and the candidate generator's stem rule (RFC 085,
  "Sub-package identity"), and make the seven `Source` readers ask their real question.
- Audit the redirect contract against `CatalogueId` once it is authoritative (RFC 085 next step 3).
- Provision a production key on the internal usage plan.

### Pending outside this repo

- catalogue-pipeline [#3796](https://github.com/wellcomecollection/catalogue-pipeline/issues/3796):
  the mets lowercasing. Until fixed, and for every row minted before it is, casing recovery stays.
- iiif-builder-infrastructure [#84](https://github.com/wellcomecollection/iiif-builder-infrastructure/pull/84):
  staging keys and stage host for all four services.
- Wellcome's answer on the multi-volume suffix convention for HRID packages (RFC 091 open question 1),
  which decides what the parser's stem rule has to split.
