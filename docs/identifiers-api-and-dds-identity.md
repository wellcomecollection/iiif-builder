# The Identifiers API and the DDS identity service

How and when the DDS should use the catalogue's Identifiers API (RFC 089), how that service relates
to the DDS's own identity service (RFC 081), whether it can make our service more authoritative, and
how to get from any identifier to the preserved files in the storage service.

**Status:** design notes, 2026-10-07. The client (`IIdentifiersApi`) and the dashboard page
(`/dash/Identifiers`) exist; nothing in the DDS's own identity service calls the API yet.

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
| Public work `identifiers` list carries the preservation identifier | `sierra-system-number b14658197` (no `mets` entry on this one, worth noting) | `mets artcoo/b/15`, `calm-ref-no ARTCOO/B/15`, `calm-altref-no ART/COO/B/15`, `sierra-system-number b30175860`, `axiell-guid …` |

Things to hold on to:

- **The `mets` row is the preservation identifier, lowercased.** The pipeline mints the METS work's
  identity from the METS record identifier, and today (and under the Folio HRID recommendation) that
  is the same string as the storage `externalIdentifier`. RFC 091 open question 2 is the only case
  where they would diverge. `IdentifierSet.PreservationIdentifier` in the client encodes this.
- **The METS work always has its own canonical id**, never the public work's (RFC 091, "METS works
  keep their own id"). So the registry can take you from a preservation identifier to *a* canonical id,
  but only the catalogue API's redirect takes you on to the public work.
- **Not every registry canonical id is a work.** `calm-ref-no/ARTCOO/B/15` has a canonical id because
  the Minter mints one for every identifier on a record, but it is not a work and the catalogue API
  404s on it. The resolve page should treat such hits as corroboration, not as a destination.
- **The `mets` value is lowercased, and that loses information the storage service needs.** The
  METS transformer lowercases the record identifier
  ([`MetsData.scala`](https://github.com/wellcomecollection/catalogue-pipeline/blob/main/pipeline/transformer/transformer_mets/src/main/scala/weco/pipeline/transformer/mets/transformer/MetsData.scala))
  because Goobi writes b numbers as `B20442233` while Sierra has `b20442233`; the code comment
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

## What exists now

- `Wellcome.Dds/Identifiers/`: `IIdentifiersApi`, the response models, `IdentifierCandidates`
  (shape-based candidate generation, which RFC 085 asks for by name), `IdentifierResolution`.
- `Wellcome.Dds.Repositories/Identifiers/WellcomeCollectionIdentifiersApi.cs`: the HTTP client.
  A 404 is `null`; anything else non-2xx throws `IdentifiersApiException` with the API's error body.
- `/dash/Identifiers`: explicit forward and reverse lookups. `/dash/Identifiers/Resolve/{id}`: tries
  every candidate tuple for an arbitrary string and leads with the preservation identifier if a `mets`
  row was found. Linked from the Manifestation and Identity pages.
- Config section `IdentifiersApi` (`BaseUrl`, `ApiKey`). Only the dashboard registers the client.
  The dev key is in the git-ignored `appsettings.Development.json`; production and staging have no key yet.

## How and when the DDS should use it

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
3. The public work's identifiers, or a forward lookup of its canonical id, say which source system
   the record came from (`sierra-system-number`, `calm-record-id`, `folio-instance`, `axiell-guid`).
   That is what `ValidateSource` was waiting for, and it is the first authoritative value `Source`
   will ever have had.

Two things make this additive rather than a replacement:

- **Pipeline lag.** The DDS and the METS adapter both react to the same storage notification. On a
  first ingest the `mets` row may not exist yet when the DDS processes the message. The write path
  must treat a 404 as "not yet", keep the search-based `CatalogueId`, and try again on the next
  reprocess (known-generator messages always re-take the write path, so this happens naturally) or
  on a scheduled re-check of rows with `SourceValidated = false`.
- **Merged works aggregate several objects.** A public work can carry more than one `mets`
  identifier. `CatalogueId` is a many-to-one binding and that is fine; the reverse direction (work to
  preservation identifier) is one-to-many and belongs to the catalogue API, not to the DDS table.

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
| `Source` | Nothing; always unvalidated | **Yes.** The source system of the public work's original row, or of the row that resolved. |
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
| `GetPreservationIdentifiersForWork(string workId)` | The work's `identifiers` of type `mets`, i.e. every storage object merged into it. The one-to-many direction. | Catalogue API |
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
     fetch the work from the catalogue API and read its `mets` identifiers; there may be several or
     none.
   - A source identifier (Folio UUID, Axiell GUID, CALM record id, HRID): reverse lookup with
     siblings to get the canonical id, then as above. For a Folio id prefer the predecessor rule:
     a `sierra-system-number` sibling is the preservation identifier.
   - Anything else: candidates, and take whichever `mets` row answers.
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

- **Keys and throttles.** Production and staging need keys on the internal plan before anything but
  the dashboard uses the client.
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

  To do: raise with the pipeline team as a defect in the Archivematica assumption, and ask whether
  the registry could carry the record identifier's original casing for new rows. Until then, treat
  `IdentifierSet.PreservationIdentifier` as an identity, never as a key.
- **`folio-instance-hrid` rows.** RFC 089 open question 2 says storing the HRID alongside the UUID is
  an optimisation the Minter *could* do. If it doesn't, the HRID is only reachable through Folio.
- **`calm-ref-no` canonical ids** are not works. The resolve page should label them.
- **Axiell ref stability** (RFC 091 open question 3) decides whether born-digital ever needs the
  predecessor path.
- **Sub-package candidate stems** (RFC 085): probe `in00012345` when `in00012345_0002` is not known.
  Not implemented.

## Next steps

1. Add the `mets` lookup and catalogue redirect to the identity write path, behind a config flag,
   writing `Source` and `CatalogueId`. Keep the search fallback. Log disagreements loudly.
2. Build the helper service and use it on the resolve page, including the storage probe.
3. Add the HRID shape to the parser and the candidate generator's stem rule.
4. Provision keys; put the DDS on the internal usage plan.
5. Audit the redirect contract against `CatalogueId` once it is authoritative (RFC 085 next step 3).
