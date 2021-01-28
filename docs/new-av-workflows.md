
## New AV Workflow

The DDS METS-Repository model follows the METS structure quite closely.

This means that an old AV workflow example, like b16675630, comprises an anchor XML file and two manifestation METS XML files.
This is reflected in the DDS Database; b16675630 has two rows, one for each manifestation.

For 2012-2019, it made sense to assume that a manifestation has a distinct type, shared by all the files in that manifestation. For b16675630, the first manifestation has type "Video" and the second manifestation has type "Transcript". These values are taken from the METS. The first manifestation has a sequence of one mpg file, the second has a sequence of one pdf.

The _original_ introduction of MXF into the workflow in early 2020 (?) added some complexity. Before, all files in the `Sequence` property of a manifestation were for use in IIIF generation. The MXF file is not for use by the DDS and DLCS and should be ignored. To support this, a second property `SignificantSequence` was added, leaving the original `Sequence` as-is. DDS code responsible for DLCS synchronisation and IIIF generation was then changed to use the new property, instead of `Sequence`. The new property is a filtered version of the old `Sequence` property, omitting MXF files.

The old `Sequence` property was not changed to `SignificantSequence` behaviour. You can still see the full sequence in the dashboard (that is, the MXF file is present in the dashboard view, because it's part of the model; it's greyed out in the UI to show that it is ignored).

In this first introduction of MXF, the MXF file for preservation and the mp4 file for access are distinct files in the METS physical file sequence.

There is still some uncecessary complexity in the way the whole codebase uses SignificantSequence, see note 1 below.

### Poster images

The DDS METS Repository model is not simply a .NET object model for the METS spec. It is Wellcome-specific, and has many helpers in implementation that allow it to be tied to resolvable files (the IPhysicalFile interface) - initially on disk at Wellcome, now in S3 buckets managed by the Wellcome Storage service. Parsing a Wellcome METS file using this model quickly gives you access to file I/O streams to do useful things. 

An even more Wellcome-specific feature is the IManifestation::PosterImage property. This isn't an IPhysicalFile, because it isn't, at present, in the METS physical files section; it is an IStoredFile, meaning it can be resolved. Poster images in the old workflow are not part of the .Sequence (and therefore not part of the SignificantSequence either, obviously). They are an afterthought, not described by METS. However, they do appear in the migrated METS, but only in the technical metadata files in the element `<mets:techMD ID="AMD_POSTER">...</mets:techMD>`. This was simply a mechanism to get them migrated; issue #1488 is the eventual real solution to this problem. Existing migrated METS will stay as-is, unless a particular b number is reprocessed for some reason.

The DDS serves poster images itself - it asks the manifestation for its PosterImage property and streams a (resampled) version as an HTTP response. (See Note 2)

Transcripts ARE part of both the Sequence and the SignificantSequence, because they are proper files in the METS, **and** they are synced with the DLCS.

### New workflow challenge

We need to keep supporting METS files that look like the old ones.
But now we'll be finding poster images and transcripts in the same manifestation as videos.

This is a big change - but how much of the current METS model approach needs to change?

We could make _Transcript_ a property like PosterImage, a special case. They feel like equivalent special cases. A new video would be a single manifestation, of type Video (as now). It would not get two entries in the DDS Database. But the manifestation has PosterImage and Transcript properties. This would be an evolution of the current approach but is probably not the right approach as explained below.

Old workflow transcripts should still be modelled in the multiple-manifestation style, at least in the first pass at this implementation, rather than changing the approach at the raw METS => DDS Model level. The raw METS is still a multiple manifestation, so the DDS internal model should be too.

The IIIF end-result of old and new workflows will look exactly the same; the handling of the different workflow models happens in the DDS Model => IIIF stage. But there would now be two ways to find a transcript.


### Future worries

The DDS model can accomodate poster images and transcripts as special adjunct properties of IManifestation. Both the poster image and the transcript are a form of derivative of the Video or Audio asset, they aren't the thing preserved. They are now formally described in METS alngside that thing, and live with it in storage. But suppose a manifestation had 2 videos, a PDF, some JP2s and an audio file, all of which as significant archival objects and not derivatives. Here, what would it mean for a manifestation to have a Type?

We can cross that bridge _if_ we come to it, which seems unlikely; the benefit of regarding whole manifestations as comprising objects of the same type outweigh the costs of generalising it back down to some universal METS model.

We can avoid some of this worry if we make a bigger change to the notion of IPhysicalFile.
We already allow IPhysicalFile to reference more that one real-world file - this is how ALTO works, the IPhysicalFile has `RelativePath` and `RelativeAltoPath` which are used by an IWorkStore implementation to obtain a file stream.

If we decide that RelativePath is the **access** file, and RelativeAltoPath is one of the other **adjunct** files, then we could have `RelativeMasterPath`, `RelativePosterPath` and `RelativeTranscriptPath` as other kinds of adjunct real files for a PhysicalFile. This fits the METS model better. The details of this are explained below.

I think at this stage it is better to add these three new RelativeXXXPath properties rather than generalise further. But we will see.



Notes

1. SignificantSequence and ignored items

This needs tidying up:

manifestation.IgnoredStorageIdentifiers
manifestation.SignificantSequence is _defined by_ manifestation.IgnoredStorageIdentifiers

But in DashboardRepository.cs, the code used the ignored storage identifiers to filter, when building up the syncoperation, rather than just iterating the SignificantSequence. Do we _actually_ need to leak the ignoring logic here?

2. We could register the poster image with the DLCS too. We don't really want an image service for it though - but we could register it anyway and just make use of the thumbnail path in the UI; this might offer more flexibility as the DDS isn't just serving up its own fixed resize.


## From the METS point of view

#### Old version

_(transcript in a second manifestation, and poster image only mentioned in techMD)_

```
  <mets:fileSec>
    <mets:fileGrp USE="OBJECTS">
      <mets:file ID="FILE_0001_OBJECTS" MIMETYPE="video/mpeg">
        <mets:FLocat LOCTYPE="URL" xlink:href="objects/0055-0000-3718-0000-0-0000-0000-0.mpg" />
      </mets:file>
    </mets:fileGrp>
  </mets:fileSec>
  <mets:structMap TYPE="LOGICAL">
    <mets:div ID="LOG_0002" LABEL="African sleeping sickness" TYPE="MultipleManifestation">
      <mets:mptr LOCTYPE="URL" xlink:href="b16675630.xml" />
      <mets:div ADMID="AMD" DMDID="DMDLOG_0001" ID="LOG_0003" LABEL="African sleeping sickness" ORDER="1" TYPE="Video" />
    </mets:div>
  </mets:structMap>
  <mets:structMap TYPE="PHYSICAL">
    <mets:div DMDID="DMDPHYS_0000" ID="PHYS_0000" TYPE="physSequence">
      <mets:div ADMID="AMD_0001" ID="PHYS_0001" ORDER="1" ORDERLABEL=" - " TYPE="page">
        <mets:fptr FILEID="FILE_0001_OBJECTS" />
      </mets:div>
    </mets:div>
  </mets:structMap>
  <mets:structLink>
    <mets:smLink xlink:from="LOG_0003" xlink:to="PHYS_0001" />
  </mets:structLink>
```

#### New version


```
  <mets:fileSec>
    <mets:fileGrp USE="ACCESS" ID="OBJECTS">
      <mets:file ID="FILE_0001_OBJECTS" MIMETYPE="video/mp4" ADMID="AMD_0001">
        <mets:FLocat LOCTYPE="URL" xlink:href="objects/b30496160_0002.mp4" />
      </mets:file>
    </mets:fileGrp>
    <mets:fileGrp USE="POSTER" ID="POSTER IMAGE">
      <mets:file ID="FILE_0001_POSTER" MIMETYPE="image/jpeg" ADMID="AMD_0003">
        <mets:FLocat LOCTYPE="URL" xlink:href="objects/b30496160_0001.jpg" />
      </mets:file>
    </mets:fileGrp>
    <mets:fileGrp USE="PRESERVATION" ID="MASTERS">
      <mets:file ID="FILE_0001_MASTERS" MIMETYPE="application/mxf" ADMID="AMD_0002">
        <mets:FLocat LOCTYPE="URL" xlink:href="objects/b30496160_0003.mxf" />
      </mets:file>
    </mets:fileGrp>
    <mets:fileGrp USE="TRANSCRIPT" ID="TRANSCRIPT">
      <mets:file ID="FILE_0001_TRANSCRIPT" MIMETYPE="application/pdf" ADMID="AMD_0004">
        <mets:FLocat LOCTYPE="URL" xlink:href="objects/b30496160_0004.pdf" />
      </mets:file>
    </mets:fileGrp>
  </mets:fileSec>
  <mets:structMap TYPE="LOGICAL">
    <mets:div ADMID="AMD" DMDID="DMDLOG_0000" ID="LOG_0000" LABEL="Anaesthesia in the horse, cow, pig, dog and cat." TYPE="Video" />
  </mets:structMap>
  <mets:structMap TYPE="PHYSICAL">
    <mets:div DMDID="DMDPHYS_0000" ID="PHYS_0000" TYPE="physSequence">
      <mets:div ID="PHYS_0001" ORDER="1" ORDERLABEL=" - " TYPE="MP4">
        <mets:fptr FILEID="FILE_0001_OBJECTS" />
        <mets:fptr FILEID="FILE_0001_POSTER" />
        <mets:fptr FILEID="FILE_0001_MASTERS" />
        <mets:fptr FILEID="FILE_0001_TRANSCRIPT" />
      </mets:div>
    </mets:div>
  </mets:structMap>
  <mets:structLink>
    <mets:smLink xlink:to="PHYS_0001" xlink:from="LOG_0000" />
  </mets:structLink>
```

Observations:

* There is no USE=OBJECTS any more to identify the file group. This becomes `<mets:fileGrp USE="ACCESS" ID="OBJECTS">`
* Each "Physical File" in the new example is actually 4 (in this case) files - 2 (or even 3) of which we need to sync with the DLCS. This makes the processing of SignificantSequence more complicated - what goes in the sequence? But - we already handle ALTO files in this way. Careful extension of IPhysicalFile should cope with new variations on the ALTO theme - more types of derivative alongside. Our focus is on the access copy, hence the use of the term adjunt rather than derivative; the access file is a derivative of the preservation file, but we're only interested in access. "Access" here is misleading, because the access is further mediated by the DLCS; for images the JP2 is used for both preservation and access, with an additional IIIF Image API layer intervening. 
* The link from a file to its ADM happens on the mets:file element, not on the div that contains the file pointers:

Old: 
```
                |--------------|     
      <mets:div ADMID="AMD_0001" ID="PHYS_0001" ORDER="1" ORDERLABEL=" - " TYPE="page">
        <mets:fptr FILEID="FILE_0001_OBJECTS" />
      </mets:div>

      ...points to:

      <mets:file ID="FILE_0001_OBJECTS" MIMETYPE="video/mpeg"                >
        <mets:FLocat LOCTYPE="URL" xlink:href="objects/0055-0000-3718-0000-0-0000-0000-0.mpg" />
      </mets:file>
```

New:
```
                |--------------|
      <mets:div                  ID="PHYS_0001" ORDER="1" ORDERLABEL=" - " TYPE="MP4">
        <mets:fptr FILEID="FILE_0001_OBJECTS" />
        <mets:fptr FILEID="FILE_0001_POSTER" />
        <mets:fptr FILEID="FILE_0001_MASTERS" />
        <mets:fptr FILEID="FILE_0001_TRANSCRIPT" />
      </mets:div>

      ...each point to (for example)
                                                             |--------------|  
      <mets:file ID="FILE_0001_OBJECTS" MIMETYPE="video/mp4" ADMID="AMD_0001">
        <mets:FLocat LOCTYPE="URL" xlink:href="objects/b30496160_0002.mp4" />
      </mets:file>
```

When looking for file metadata, PhysicalFile should look in both places.
 - does the `mets:div` _container_ of the file (or files) have the `ADMID` property?
 - if not, follow the file pointer and see if that has the ADMID

This means, though, that the technical metadata no longer belongs to the IPhysicalFile directly but to its component files individually.

I think we need to extend our model for PhysicalFile to accommodate multiple actual stored files. This keeps things more aligned with the METS rather than trying to produce a fake sequence, but complicates the SyncOperation; the files to sync aren't a subset of the Sequence any more; some of them are adjuncts of the access file we're already syncing.

### Poster images in the DLCS

Seeing as we're going to have to deal with transcripts being synced to DLCS we may as well sync poster images to the DLCS too, when they come via this workflow. The benefit of this is that it allows for access control to be applied.


### Testing

In iiif-builder we got rid of EustonFileSystemWorkStorageFactory and EustonFileSystemWorkStore implementations of IWorkStorageFactory and IWorkStore because we no longer needed them. However, I think testing this would be helped by the simplest possible local filesystem based impls of these. They will not require a storage map - they aren't bag-aware, just file-aware.
