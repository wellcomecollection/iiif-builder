# PDF cover page

When the DLCS builds a PDF of a digitised work (`https://iiif.wellcomecollection.org/pdf/{identifier}`,
the Manifest's `rendering`), it prepends a cover page that it fetches from the DDS:

    GET /pdf-cover/{identifier}

The page is built from the work's IIIF Presentation 3 Manifest in the presentation bucket (`v3/{identifier}`):
the title, Contributors, Publication/Creation, the persistent URL, any additional providers, the
`requiredStatement` (or the "Attribution and usage" metadata when there is none), and a footer with the
provider's logo and address. A 404 means there is no Manifest for the identifier.

## Implementation

`Wellcome.Dds.Server`'s `PdfCoverController`, backed by `Wellcome.Dds.Repositories/Pdf/CoverPage`:
content extraction, text handling, and a PdfPig writer with the Inter fonts embedded in the assembly.
Pure managed code, an Apache-2.0 library, no extra container. Tests leave `cover-page-sample.pdf` next
to the test assembly for eyeballing.

It answers at:

- `/pdf-cover/{identifier}`, the path the DLCS uses;
- `/pdf-cover-v2/{identifier}`, the same page, added so the edge could route the new implementation by
  path while the old one still ran;
- `/pdf-cover/ping`, for anything that probes the old health check.

The footer logo is the Wellcome Collection PNG embedded in the Repositories assembly, a copy of the one
the server serves at `/logos/` and names in every Manifest. The DDS writes that provider block itself, so
it is always this logo and nothing is fetched.

## History

Until November 2026 the cover page came from a separate Flask and reportlab service, `src/PdfCoverPage`
(image `Dockerfile-pdfgenerator`, ECS service `pdf-generator` at `pdf.wellcomecollection.digirati.io`).
The server's implementation replaced it in #304, with the same paths and the same output. The Python code
and its Dockerfile have been removed from this repository.

The running service was retired on 18 November 2026. iiif-builder-infrastructure #88 sent `/pdf-cover*` on
`pdf.wellcomecollection.digirati.io` (and `pdf-stage.` on staging) to `Wellcome.Dds.Server`, and removed the
`pdf-generator` service from staging and production. #89 removes its ECR repository. Wellcome's CloudFront
still sends `/pdf-cover*` on `iiif.wellcomecollection.org` to that hostname, which is how the DLCS reaches it.
