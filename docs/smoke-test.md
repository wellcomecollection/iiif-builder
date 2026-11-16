# Smoke tests

What to check after a deploy, on staging first and then production. Unit tests cover the logic; smoke
tests cover what only a running system shows: native libraries in the images, external tools, fonts,
logins, database access, the DLCS, and the edge in front of it all.

Half of it is automated, and runs on its own after every deploy. The rest needs a person, a login or the
logs, and is the checklist below.

| Environment | Server | Dashboard |
| --- | --- | --- |
| Staging | `https://iiif-stage.wellcomecollection.org` | `https://iiif-stage.wellcomecollection.org/dash` (direct: `dds-stage.wellcomecollection.digirati.io`) |
| Staging-prod | `https://iiif-test.wellcomecollection.org` (shuts down at 18:00) | `https://iiif-test.wellcomecollection.org/dash` |
| Production | `https://iiif.wellcomecollection.org` | `https://iiif.wellcomecollection.org/dash` |

Staging and staging-prod have only what has been ingested into them; a 404 for an identifier that exists
in production usually means that, not a fault.

## Automated: the Smoke test workflow

`src/Wellcome.Dds/Wellcome.Dds.SmokeTests` checks a deployed environment from outside, with no credentials.
The Deploy workflow runs it once the services are stable (`.github/workflows/smoke-test.yml`); it can also
be run by hand from Actions → Smoke test, for any environment. It only reports: failures show in the run
summary and as annotations, but don't fail the deploy.

It checks the environment's health endpoint first. If that doesn't answer (staging-prod shuts down at
18:00), the run summary says the smoke test was skipped, rather than reporting every check as failed.

What it checks:

| Area | Checks |
| --- | --- |
| Health | Server and dashboard health checks say `Healthy` (the dashboard's includes the databases); Swagger loads; the dashboard sends an anonymous user to the Entra login, with a redirect back to this host's `/dash/signin-oidc` |
| IIIF | Goobi manifest as Presentation 3 and 2; the top-level collections (v3, v2 and archives) list items; a Collection leads to Manifests (two levels for a multi-volume work, three for Chemist & Druggist); born-digital manifest; AV manifest has video or sound |
| Text | ALTO resolves for the first, middle and last canvas; their line annotation pages and the manifest's image annotation page resolve; the raw text rendering resolves; search within finds hits, and autocomplete suggests terms |
| Image access (DLCS) | An open item has no `maxWidth` and serves tiles; a size-limited item (player flags, e.g. the genetics books) has `maxWidth: 1000`, serves `/full/!1000,1000/` but refuses tiles; an access-controlled item offers auth services and refuses anonymous tiles, its Auth 2 probe says 401 and its access service page loads; thumbnails are served for all three |
| Rendering | Placeholder canvases are 1000×800 PNGs (libvips), both directly and as painted in a born-digital manifest; the AV poster thumbnail scales to 300 wide keeping its aspect ratio; a born-digital PDF thumbnail renders at 200 and 400 wide (pdftoppm) and 2000 is refused |
| Cover page | `/pdf-cover-v2/{id}` is one page, made by PdfPig, with Inter embedded, and the persistent URL and licence are links; ping answers; an unknown item is 404 |

Each environment's items are in `Fixtures/{environment}.json`. A check whose item isn't set there is
skipped, not passed; staging and staging-prod skip the ones they have no content for. To add a check's item
for an environment, ingest something suitable there and add its identifier.

Choose staging items that staging itself built after May 2023. Until then, staging's configuration put
other hosts in what it built: from March 2021 to April 2023 staging-prod wrote into staging's buckets with
`iiif-test` ids (that is how staging got Chemist & Druggist and `b18035978`), and until May 2023 staging's
own DLCS links used `iiif-test` (as in `b31486320`'s images). Those items still work on staging only as far as
staging-prod answers for them, which is not at all after 18:00, so the checks would test staging-prod. Look
at an item's JSON first: every `iiif-…` host in it should be `iiif-stage`. Chemist & Druggist is never
rebuilt anywhere, so staging has no collection check.

To run locally (the tests call the live services):

```bash
cd src/Wellcome.Dds
SMOKE_ENV=staging dotnet test Wellcome.Dds.SmokeTests/Wellcome.Dds.SmokeTests.csproj
# SMOKE_HOST overrides the fixture file's host, e.g. to test the dashboard's direct address
```

They're tagged `Category=Smoke`, and the unit-test run in CI leaves them out.

## Manual: every deploy

- [ ] The Smoke test run (in the Deploy run, or run by hand) has no failures, or only ones you understand.
- [ ] Workflow processor and job processor: healthy in ECS, and their logs show startup without exceptions.
- [ ] Workflow processor log has `PDF thumbnails will be rendered by pdftoppm at /usr/bin/pdftoppm`
      (the image has poppler-utils; `ghostscript` or `cannot be generated` here means it does not).
- [ ] Server log has no `Fontconfig` errors at startup or on the first placeholder canvas request, and no
      `libvips` or `VipsOperation` errors after the smoke test.

## Image rendering: after any change to NetVips, the images, fonts or the base image

- [ ] Placeholder canvas text is in Wellcome Bold, not a fallback font:
      `{host}/extensions/born-digital/placeholder-canvas/x-fmt/92/image/vnd.adobe.photoshop`.
      Expect two headings ("File type:", "PRONOM key:") at 32px and two sentences at 16px; the long MIME type
      in `.../fmt/412/application/vnd.openxmlformats-officedocument.wordprocessingml.document` wraps.
- [ ] (After a base image change) the published image contains `runtimes/linux-x64/native/libvips.so.42`
      and no `win-x64` natives: `docker run --rm --entrypoint ls <image> /app/runtimes`.

## Born-digital PDF thumbnails: after any change to the thumbnail service, poppler, or the base image

- [ ] On demand: the first request for an item with no stored thumbnail logs
      `On-demand PDF thumbnail for '...': ...: Generated`; the second is served from the stored thumbnail
      (no `Rendering page 1` log line).
- [ ] Access rule: a PDF whose access condition is not Open, Requires registration or Open with
      advisory returns 404, and the log says `not served: not a PDF whose access condition permits it`.
      Check that this line does NOT appear for an item that should be open.
- [ ] Workflow processor: reprocess a born-digital package from the dashboard (Workflow call → RebuildIIIF);
      the log shows `PDF thumbnail: ... Generated` (or `AlreadyExists` / `NotPermitted`) per manifestation
      and never fails the job for a thumbnail problem.
- [ ] Dashboard `/dash/Upload/PdfThumb/{id}`: page shows the stored thumbnail image; "Generate" replaces it
      and the success message names pdftoppm; uploading a JPEG replaces it and the preview changes.
- [ ] Repeated requests for an identifier that cannot be rendered (no PDF, too large, broken file) are
      answered from memory for ten minutes (`produced nothing ... not retrying until` in the log once).

## PDF cover page (PdfPig): after any change to the cover page, PdfPig, or the fonts

- [ ] `{host}/pdf-cover-v2/b31486320` looks right: title, Contributors, Publication/Creation, Persistent URL,
      "License and attribution", and the Wellcome Collection logo and address in the footer.
- [ ] A link that wraps across a line is clickable on both lines (`b28942772`, two links in the attribution
      text). The smoke test checks that the persistent URL and the licence are links.
- [ ] A title with accented characters renders them (`b18035978` has none; find one with é or an em dash).
- [ ] Until the edge routes `/pdf-cover/...` to the server, it still goes to the Python service, and the DLCS
      may call `pdf.wellcomecollection.digirati.io` directly (see #315). After: a DLCS-generated PDF
      (`{host}/pdf/b31486320`) has the server's cover page (producer "PdfPig") as page 1.

## Dashboard: every deploy, and after any change to Microsoft.Identity.Web, EF Core or the database

- [ ] Login via Entra works: after Microsoft's login you're back on the dashboard, the footer shows the
      account and links to the build page, which shows the deployed commit. (The smoke test checks the
      redirect to the login.)
- [ ] Home page lists recent items; search box finds `b18035978`.
- [ ] Manifestation page for a Goobi item, a multiple-manifestation volume and a born-digital item.
- [ ] Workflow: `/dash/WorkflowCall/Stats`, `/dash/WorkflowCall/Recent`, `/dash/WorkflowCall/Errors`.
- [ ] Jobs and queue pages: `/dash/Job/Recent`, `/dash/Dash/Queue`, `/dash/Dash/Status`.
- [ ] Identity: `/dash/Identity/Get/b18035978` shows the record; `/dash/Identity/Stats` loads with counts
      (this page runs aggregate queries nothing else runs).
- [ ] Identifiers API: `/dash/Identifiers` shows the "not enabled" banner where there is no key
      (production) or resolves `b18035978` where there is (staging); `/dash/Identifiers/Resolve/ARTCOO/B/15`
      shows DDS identity, registry (staging), public work and storage probes without an error.
- [ ] Log viewer `/dash/Log` and user activity `/dash/Dash/RecentActions` load.
- [ ] Upload page `/dash/Upload/PdfThumb/{id}` (see above).

## Workflow and ingest: every deploy

- [ ] Trigger a reprocess of a Goobi item from the dashboard; the workflow processor picks it up,
      the job finishes without error, the manifest's `Updated` changes.
- [ ] A Goobi message carrying a volume identifier (`b…_0001`) is normalised to the package (log line
      from `WorkflowProcessorService`), while `Dds:AcceptManifestationLevelWorkflowIdentifiers` is true.
- [ ] Born-digital reprocess as above, with thumbnails.
- [ ] Job processor: a DLCS ingest job progresses (dashboard queue page), nothing stuck in `Taken`.
- [ ] The `identities` table gets a new or updated row for the reprocessed item, with `catalogue_id` set
      (dashboard identity page shows it; `source_validated` is false by design).

## Authentication and roles: after any change to Auth.Web, the Sierra client, or ASP.NET Core

- [ ] The smoke test checks the anonymous side: auth services offered, the Auth 2 probe says 401, the
      clickthrough access page loads.
- [ ] Role provider login (`/wlorgp` / `/wlorgr` flow from a DLCS-authenticated item) authenticates a
      known reader account against the Sierra REST patron API and returns the expected roles.
- [ ] The DLCS basic-auth endpoints the DLCS calls (origin fetch with `DlcsOriginUsername`) still work:
      an image ingest completes.

## Log lines worth grepping after any deploy

| Line | Meaning |
| --- | --- |
| `not served: not a PDF whose access condition permits it` | expected for restricted PDFs; a problem on open ones |
| `Identifiers query for '...' is ambiguous` | the catalogue returned several equally-bound works; rare |
| `ROUTES DISAGREE` | registry and catalogue search chose different works (staging only, where the registry is enabled) |
| `PDF thumbnail generation failed` | pdftoppm or S3 problem; the job carried on |
| `Fontconfig` / `Pango` | font setup in the server image |
| `Could not load METS` | storage service or identifier problem |

## Notes for the .NET 10 move (November 2026)

The framework, EF Core (6 → 10), Microsoft.Identity.Web (1 → 4), FeatureManagement (2 → 4),
Swashbuckle (6 → 10), the Postgres distributed cache (3 → 6) and Testcontainers all changed at once, and
the images moved from Debian bookworm to Ubuntu noble. So on that deploy, every section above applies,
with particular attention to: the dashboard login (Identity.Web), every dashboard list page (EF Core
query translation), the Swagger page, the `__dist_cache` table being written by the server (the cache
package), and the libvips and pdftoppm items (new base image).
