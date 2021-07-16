# Thumbnails and other derived images

The DLCS has a thumbnail service for all **Image** assets. 

It does not have a thumbnail service for audio, video or document (e.g., PDF) assets.

Examples:

* https://dlcs.io/thumbs/wellcome/5/b16754256_0001.jp2 (Image Service ID)
* https://dlcs.io/thumbs/wellcome/5/b16754256_0001.jp2/full/,400/0/default.jpg (a particular sized thumb)

With Cloudfront, this is the path `iiif.wellcomecollection.org/thumbs`:

* https://iiif.wellcomecollection.org/thumbs/b16754256_0001.jp2

While you could use the regular IIIF Image service (/iiif-img/) for these, the advantage of using the /thumbs/ service is that no image server is touched by the request. They are _fast_ and you can make hundreds of concurrent requests.

The format is simple:

* https://iiif.wellcomecollection.org/thumbs/{asset-id}

where {asset-id} is derived from METS, and is _usually_ the same as the asset filename on disk [1].

Examples:

* https://iiif.wellcomecollection.org/thumbs/b16754256_0001.jp2/full/!400,400/0/default.jpg
* https://iiif.wellcomecollection.org/thumbs/b20274282_ms_6309_2_0010.JP2/full/!100,100/0/default.jpg

The Catalogue API includes references to thumbnails for works. For many works, this is the DLCS /thumbs/{asset-id} URL for the first image. And if there are multiple manifestations, the work will have the first image in the first manifestation.

For printed books that have had their title pages identified in the METS logical structMap, the Catalogue API uses the corresponding `/thumbs/{asset-id}` for that asset, which isn't the first.

The DDS assigns thumbnails to IIIF Manifests, and it **uses the thumbnail that the Catalogue API has determined for the work**.

So this manifest has the fifth image as its thumbnail, rather than the first:

https://iiif.wellcomecollection.org/presentation/b21528184 => https://iiif.wellcomecollection.org/thumbs/b21528184_0005.jp2/full/61,100/0/default.jpg

## Images provided by the DDS (IIIF-builder)

There are other images available for works, besides image-derived thumbnails.

* Video and audio can have poster images - an additional jpeg file included in the METS.
* PDFs can have a thumbnail, made by rasterising the first page of the PDF (converting it to a small jpeg)

The DLCS doesn't know about these. They are currently served by the DDS (i.e., iiif-builder).
For AV, this image doesn't correspond to the AV asset ID in the METS. It's a different image, and isn't even in the DLCS.

### The DDS /thumb/ endpoint

The job of this endpoint is different from the DLCS /thumbs/ endpoint. 

Whereas the DLCS /thumbs/ takes an asset identifier as a parameter, the DDS /thumb/ service takes a work or manifestation identifier as a parameter.

It returns an appropriate image for the _work_ or _manifestation_ - even if it's AV.

This works for regular DLCS JP2s as well:

https://iiif.wellcomecollection.org/thumb/b21528184 (an image-based work)

...redirects to the DLCS path for the cover page of this work [2]

but

https://iiif.wellcomecollection.org/thumb/b16659090 (a video)

...directly serves the poster image JPEG (although it does normalise the size)

and

https://iiif.wellcomecollection.org/thumb/b32273411 (a PDF)

...serves the jpeg bitmap first page of the PDF.

**These are not being served by the DLCS.**

This service can be called with just a b number and always be expected to work:

https://iiif.wellcomecollection.org/thumb/{bnumber}

But it can also take a _manifestation identifier_. Consider the video again:

https://iiif.wellcomecollection.org/thumb/b16659090

This is a video with a transcript, so there are actually two possible thumbnails here. We can be more specific about which one we want:

* https://iiif.wellcomecollection.org/thumb/b16659090_0001
* https://iiif.wellcomecollection.org/thumb/b16659090_0002

These identifiers are derived from METS for multiple manifestations:

```
  <mets:structMap TYPE="LOGICAL">
    <mets:div ADMID="AMD" DMDID="DMDLOG_0000" ID="LOG_0000" LABEL="The story of the Wellcome Foundation Ltd" TYPE="MultipleManifestation">
      <mets:div ID="LOG_0001" LABEL="The story of the Wellcome Foundation Ltd" ORDER="1" TYPE="Video">
        <mets:mptr LOCTYPE="URL" xlink:href="b16659090_0001.xml" />
      </mets:div>
      <mets:div ID="LOG_0002" LABEL="The story of the Wellcome Foundation Ltd." ORDER="2" TYPE="Transcript">
        <mets:mptr LOCTYPE="URL" xlink:href="b16659090_0002.xml" />
      </mets:div>
    </mets:div>
  </mets:structMap>
```


## Future development of DLCS

* DLCS should provide a thumb service for PDFs
* DLCS should provide a thumb/iiif service for video keyframes (this is not the same a poster image; as poster might not be derived from the video)
* DLCS should allow one asset to have another asset associated with it (allowing audio or video to have an arbitrary poster image and have the DLCS serve them)

Even with the DLCS doing this, the DDS would still be the place to ask for a work or manifestation level thumbnail, because only the DDS understands what those concepts are.

--

## Notes

[1] See https://github.com/wellcomecollection/docs/pull/30/files

[2] So why not just put this /thumb/bnumber path into the Catalogue API and let DDS redirect appropriately? Because, at present, the DDS learns the appropriate thumb from the Catalogue API! This logic (finding the cover page) could move to the DDS, though - it already does this to generate IIIF Range Navigation.