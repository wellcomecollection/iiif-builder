using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Utils;

namespace Wellcome.Dds.Catalogue
{
    public static class WorkExtensions
    {
        public static string GetIdentifierByType(this Work work, string identifierTypeId)
        {
            var foundIdentifier = work.Identifiers.SingleOrDefault(
                id => id.IdentifierType.Id == identifierTypeId);
            return foundIdentifier?.Value;
        }
        
        public static string GetParentId(this Work work)
        {
            if (work.PartOf.HasItems())
            {
                return work.PartOf.Last().Id;
            }
            return null;
        }
        
        
        public static List<Metadata> GetMetadata(this Work work, string identifier)
        {
            var metadataList = new List<Metadata>();
            foreach(Contributor c in work.Contributors)
            {
                metadataList.Add(new Metadata(identifier, c.Type, c.Agent.Label, c.Agent.Id));
            }
            foreach (Classification c in work.Subjects)
            {
                metadataList.Add(new Metadata(identifier, c.Type, c.Label, c.Id));
            }
            foreach (Classification c in work.Genres)
            {
                metadataList.Add(new Metadata(identifier, c.Type, c.Label, c.Id));
            }
            foreach (var digcode in work.Identifiers.Where(id => id.IdentifierType.Id == "wellcome-digcode"))
            {
                var dgLabel = DigitalCollectionsMap.GetFriendlyName(digcode.Value);
                if (dgLabel == null)
                {
                    dgLabel = $"({digcode.Value})";
                }
                metadataList.Add(new Metadata(identifier, "Digitalcollection", dgLabel, digcode.Value));
            }
            var locationOfOriginal = work.Notes.FirstOrDefault(note => note.NoteType.Id == "location-of-original");
            if (locationOfOriginal != null && locationOfOriginal.Contents.HasItems())
            {
                var location = locationOfOriginal.Contents[0];
                metadataList.Add(new Metadata(identifier, "Location", location, location));
            }
            return metadataList;
        }

        public static IEnumerable<string> GetNotes(this Work work, string noteType)
        {
            return work.Notes?
                .Where(n => n.NoteType.Id == noteType)
                .SelectMany(n => n.Contents);
        }

        public static string[] GetSierraSystemBNumbers(this Work work)
        {
            var sierraId = work.Identifiers.Where(
                    i => i.IdentifierType.Id == "sierra-system-number");
                return sierraId.Select(id => id.Value).ToArray();
        }

        private static readonly Regex DigitalLocationRegex = new Regex(@"^.*\/(b[0-9x]{8})\/?", RegexOptions.IgnoreCase);
        
        public static string[] GetDigitisedBNumbers(this Work work)
        {
            var sierraSystemBNumbers = work.GetSierraSystemBNumbers();
            var iiifLocations = work.Items
                .SelectMany(item => item.Locations)
                .Where(loc => loc.LocationType.Id == "iiif-presentation")
                .ToList();
            if (iiifLocations.Any())
            {
                if (iiifLocations.Count == 1)
                {
                    var digBNum = sierraSystemBNumbers.SingleOrDefault(
                        bNumber => iiifLocations[0].Url.Contains($"/{bNumber}"));
                    if (digBNum.HasText())
                    {
                        // simplest and happy path. There is one digital location and it's the Sierra system number.
                        return new[] {digBNum};
                    }
                }
                // This seems to be the case for videos?
                // Digital Location b number is not mentioned anywhere else in the Work.
                // Parse the b number out of the digital location; this has to work for old and new
                // https://wellcomelibrary.org/iiif/b16784613/manifest
                // https://iiif.wellcomecollection.org/presentation/b16784613
                var parsedDigitalBNumbers = new List<string>();
                foreach (var iiifLocation in iiifLocations)
                {
                    var m = DigitalLocationRegex.Match(iiifLocation.Url);
                    if (m.Success)
                    {
                        parsedDigitalBNumbers.Add(m.Groups[1].Value);
                    }
                }

                return parsedDigitalBNumbers.ToArray();
            }

            return new string[0];
        }

        public static bool HasDigitalLocation(this Work work)
        {
            var iiifLocations = work
                .Items?.Where(item => item.Locations.Any(
                    location => location.LocationType.Id == "iiif-presentation"));
            return iiifLocations.HasItems();
        }

        public static bool? IsOnline(this Work work)
        {
            return work.Availabilities?.Any(a => a.Id == "online");
        }
    }
}