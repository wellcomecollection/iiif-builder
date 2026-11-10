# Smoke test checklist

What to check by hand after a deploy, on staging first and then production. Automated tests cover the
logic; this list covers the things only a running system shows: native libraries in the images,
external tools, fonts, logins, database access, and the edge in front of it all. Tick everything in
the "every deploy" sections; the others when the area has changed.

Replace `{host}` with the environment:

| Environment | Server | Dashboard |
| --- | --- | --- |
| Staging | `https://iiif-stage.wellcomecollection.org` | `https://iiif-stage.wellcomecollection.org/dash` (direct: `dds-stage.wellcomecollection.digirati.io`) |
| Production | `https://iiif.wellcomecollection.org` | `https://iiif.wellcomecollection.org/dash` |

Staging has only what has been ingested into staging; a 404 for an identifier that exists in
production usually means that, not a fault. The identifiers below are known on both unless noted.

## Every deploy: the four services are up

- [ ] Server health: `{host}/management/healthcheck` returns `Healthy`.
- [ ] Dashboard health: `{host}/dash/management/healthcheck` returns `Healthy` (database checks included).
- [ ] Workflow processor and job processor: healthy in ECS, and their logs show startup without exceptions.
- [ ] Workflow processor log has `PDF thumbnails will be rendered by pdftoppm at /usr/bin/pdftoppm`
      (the image has poppler-utils; `ghostscript` or `cannot be generated` here means it does not).
- [ ] Server log has no `Fontconfig` errors at startup or on the first placeholder canvas request.

## Every deploy: IIIF

- [ ] Goobi manifest: `{host}/presentation/b18035978` (v3) and `{host}/presentation/v2/b18035978`.
- [ ] Multiple-manifestation item: `{host}/presentation/b19974760` is a Collection; one of its volumes
      (`{host}/presentation/b19974760_10`) is a Manifest.
- [ ] Born-digital manifest: `{host}/presentation/ARTCOO/B/15` (staging: `SA/REN/T/1`).
- [ ] AV manifest with a poster: pick a current AV item from the dashboard home page.
- [ ] A Canvas's image service and a page of ALTO text resolve for the Goobi item above
      (`{host}/text/alto/b18035978/...` from the manifest's `seeAlso`).
- [ ] Search within: `{host}/search/v1/b18035978?q=health` returns hits.
- [ ] Swagger loads: `{host}/swagger`.

## Image rendering (libvips via NetVips): after any change to NetVips, the images, fonts or the base image

The server renders placeholder canvases with libvips and Pango using the bundled Wellcome Bold font.
The server, dashboard and workflow processor resize JPEGs with libvips.

- [ ] Placeholder canvas renders as a PNG with the text in Wellcome Bold, not a fallback font:
      `{host}/extensions/born-digital/placeholder-canvas/x-fmt/92/image/vnd.adobe.photoshop`.
      Expect two headings ("File type:", "PRONOM key:") at 32px and two sentences at 16px.
- [ ] A long MIME type wraps instead of overprinting:
      `{host}/extensions/born-digital/placeholder-canvas/fmt/412/application/vnd.openxmlformats-officedocument.wordprocessingml.document`.
- [ ] A born-digital manifest for an item with a non-image file shows the placeholder canvas inline.
- [ ] AV poster via `/thumb`: `{host}/thumb/{av identifier}` returns a JPEG at the requested width
      (`{host}/thumb/{id}/300` is 300 wide; a portrait poster keeps its aspect ratio, not square-fitted).
- [ ] Server log has no `libvips` or `VipsOperation` errors after the above.
- [ ] (After a base image change) the published image contains `runtimes/linux-x64/native/libvips.so.42`
      and no `win-x64` natives: `docker run --rm --entrypoint ls <image> /app/runtimes`.

## Born-digital PDF thumbnails: after any change to the thumbnail service, poppler, or the base image

- [ ] On demand: `{host}/thumb/{born-digital PDF identifier}` for an item with no stored thumbnail
      returns a JPEG (first request renders page 1 and stores it; server log:
      `On-demand PDF thumbnail for '...': ...: Generated`). Production reference: `b29696586` → 200×283 at
      the default width. Staging: `SA/REN/T/1`.
- [ ] Second request is served from the stored thumbnail (no `Rendering page 1` log line).
- [ ] `{host}/thumb/{id}/400` returns 400 wide; `{host}/thumb/{id}/2000` is rejected (max 1024).
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

- [ ] `{host}/pdf-cover-v2/b31486320` returns a one-page PDF: title, Contributors, Publication/Creation,
      Persistent URL, "License and attribution", and the Wellcome Collection logo and address in the footer.
- [ ] Compare with the old service's `https://pdf.wellcomecollection.digirati.io/pdf-cover/b31486320`
      (staging: `pdf-stage.wellcomecollection.digirati.io`): same text and line breaks; footer position may
      differ by a few points.
- [ ] Links are clickable: the persistent URL, and "Creative Commons, Public Domain Mark" inside the
      attribution text (also `b28942772`, which has two links, one wrapping across a line).
- [ ] Fonts are embedded (open the PDF's properties: Inter-Regular and Inter-Bold, embedded subsets).
- [ ] A title with accented characters renders them (`b18035978` has none; find one with é or an em dash).
- [ ] `{host}/pdf-cover-v2/ping` answers; `{host}/pdf-cover-v2/b00000000` is 404, not 500.
- [ ] Until the DLCS is switched: `{host}/pdf-cover/...` still goes to the Python service at the edge.
      After: a DLCS-generated PDF (`{host}/pdf/b31486320`) has the cover page as page 1.

## Dashboard: every deploy, and after any change to Microsoft.Identity.Web, EF Core or the database

- [ ] Login via Entra works (`/dash` redirects to Microsoft login and back; the footer shows the account).
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
