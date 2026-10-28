# PDF cover page

When the DLCS builds a PDF of a digitised work (`https://iiif.wellcomecollection.org/pdf/{identifier}`,
the Manifest's `rendering`), it prepends a cover page that it fetches from the DDS:

    GET /pdf-cover/{identifier}

The page is built from the work's IIIF Presentation 3 Manifest in the presentation bucket (`v3/{identifier}`):
the title, Contributors, Publication/Creation, the persistent URL, any additional providers, the
`requiredStatement` (or the "Attribution and usage" metadata when there is none), and a footer with the
provider's logo and address. A 404 means there is no Manifest for the identifier.

## Two implementations, for now

- **`Wellcome.Dds.Server`**, `PdfCoverController`, backed by `Wellcome.Dds.Repositories/Pdf/CoverPage`
  (content extraction, text handling, a PdfPig writer with the Inter fonts embedded in the assembly).
  Pure managed code, Apache-2.0 library, no extra container. Added October 2026. Tests leave
  `cover-page-sample.pdf` next to the test assembly for eyeballing.
- **`src/PdfCoverPage`**, the original Flask and reportlab service in its own container
  (`Dockerfile-pdfgenerator`, ECS service `pdf-generator` at `pdf.wellcomecollection.digirati.io`).
  This is what the DLCS named query currently calls.

The path and the output are the same, so switching is a DLCS configuration change: point the named
query's cover page URL at the iiif-builder host. The server also answers at `/pdf-cover-v2/{identifier}`,
so the edge can route the old path to the old service and the new path to the server while both run,
and the DLCS can be switched by path. The provider logo is fetched from the URL in the Manifest with a
User-Agent header; the logo's CloudFront rejects requests without one. Once that has happened and been verified, delete
`src/PdfCoverPage`, `Dockerfile-pdfgenerator`, and the `pdf-generator` service and ECR repository in
iiif-builder-infrastructure. `/pdf-cover/ping` exists on the server too, for anything that still probes
the old health check.
