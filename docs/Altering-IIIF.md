# Altering IIIF

Where do I go if I want to change:

 * Human-readable information included in IIIF
 * Which catalogue metadata appear in the IIIF
 * The shape of the IIIF that IIIF-Builder builds

## A note about language

One of IIIF's design principles is this:

> IIIF specifications encourage internationalization efforts by requiring that text values of properties indicate their language, to support and encourage use around the world. IIIF assumes that text values of properties may have multiple values in multiple languages, rather than this being a special case.

This means you need to state the language of any string intended for display to humans. Where this is not applicable (e.g., it's a quantitative value), or you really don't know, you have to say "none". 

You can see this reflected in the following.

## Metadata

([Spec](https://iiif.io/api/presentation/3.0/#metadata))

These are the label value pairs that IIIF uses to carry arbitrary text or HTML.

They are set here: [MetadataBuilder](https://github.com/wellcomecollection/iiif-builder/blob/master/src/Wellcome.Dds/Wellcome.Dds.Repositories/Presentation/MetadataBuilder.cs)

This translates properties of the Work, from the catalogue API, into strings for viewers and other IIIF clients to display to humans.

If there is no value, no entry is made.

This begs the question - should all work types have the same rule set? At the moment we have different rules for different types, but that is inherited from old DDS. It may be that the necessary logic about what appears for different workTypes is already sufficiently encapsulated in the Catalogue API's transformations from source data in Sierra and CALM.

## Rights and usage

At the moment, rights statements are held in the same way as old DDS, in this class:

[PlayerConfigProvider](https://github.com/wellcomecollection/iiif-builder/blob/master/src/Wellcome.Dds/Wellcome.Dds.Repositories/Presentation/LicencesAndRights/LegacyConfig/PlayerConfigProvider.cs#L78)

(The name of that class will change once migration is done).

In IIIF 3 we have the `requiredStatement` field, which assembles all the things you would really want to tell a potential user, or to fill a "Can I use this?" info box. This is built here:

[IIIFBuilderParts#L119](https://github.com/wellcomecollection/iiif-builder/blob/master/src/Wellcome.Dds/Wellcome.Dds.Repositories/Presentation/IIIFBuilderParts.cs#L119)


## Provider(s)

This is a new property in IIIF, added because institutions wanted to say a bit more about themselves.

([Spec](https://iiif.io/api/presentation/3.0/#provider))

The information about Wellcome that goes into all IIIF manifests is built here:

[ProviderExtensions](https://github.com/wellcomecollection/iiif-builder/blob/master/src/Wellcome.Dds/Wellcome.Dds.Repositories/Presentation/ProviderExtensions.cs)

If the work has come from a partner institution, they also get a provider entry, and a logo. These are specified here:

[PartnerAgents](https://github.com/wellcomecollection/iiif-builder/blob/master/src/Wellcome.Dds/Wellcome.Dds.Repositories/Presentation/PartnerAgents.cs)

There is some rather free-form logic for converting the value of the location-of-original field in the Catalogue API into one of these partners. This field is free text, hence the logic here:

[PartnerAgents#L94](https://github.com/wellcomecollection/iiif-builder/blob/master/src/Wellcome.Dds/Wellcome.Dds.Repositories/Presentation/PartnerAgents.cs#L94)


## General structure

The assembly of IIIF is concentrated in the IIIFBuilder class and its helper, IIIFBuilderParts:

[IIIFBuilder](https://github.com/wellcomecollection/iiif-builder/blob/master/src/Wellcome.Dds/Wellcome.Dds.Repositories/Presentation/IIIFBuilder.cs)  [IIIFBuilderParts](https://github.com/wellcomecollection/iiif-builder/blob/master/src/Wellcome.Dds/Wellcome.Dds.Repositories/Presentation/IIIFBuilderParts.cs)

Here's where the IIIF common to both manifests and collections gets added:

[IIIFBuilder.cs#L343](https://github.com/wellcomecollection/iiif-builder/blob/master/src/Wellcome.Dds/Wellcome.Dds.Repositories/Presentation/IIIFBuilder.cs#L343)

And the IIIF that's just for Manifests:

[IIIFBuilder.cs#L313](https://github.com/wellcomecollection/iiif-builder/blob/master/src/Wellcome.Dds/Wellcome.Dds.Repositories/Presentation/IIIFBuilder.cs#L313)

Each of these helpers is found in IIIFBuilderParts.


## JSON Appearance

The order of properties in the serialised IIIF is governed by the JsonProperty attribute that decorates most fields in the IIIF library.
For example, [ResourceBase](https://github.com/wellcomecollection/iiif-builder/blob/master/src/Wellcome.Dds/IIIF/Presentation/ResourceBase.cs)


Additional serialisation rules are added by this class:

[PrettyIIIFContractResolver](https://github.com/wellcomecollection/iiif-builder/blob/master/src/Wellcome.Dds/IIIF/PrettyIIIFContractResolver.cs)






