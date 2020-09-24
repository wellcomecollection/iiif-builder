using System.Collections.Generic;
using System.Linq;
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
    }
}