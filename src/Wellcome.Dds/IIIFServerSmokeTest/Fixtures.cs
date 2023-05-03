namespace IIIFServerSmokeTest;

public class Fixtures
{
    public List<WorkFixture> List { get; }

    public Fixtures(DateTime minimumDateTime)
    {
        List = new List<WorkFixture>()
        {
            new("b2178081x", "2 smallish volumes")
            {
                IdentifierIsCollection = true,
                ManifestCount = 2,
                HasAlto = true
            },
            new("b22454408", "Short text")
            {
                IdentifierIsCollection = false,
                HasAlto = true
            },
            new("b2043067x", "2 images no text")
            {
                IdentifierIsCollection = false,
                HasAlto = false
            },
            new("b24923333", "2 x 550 page MM")
            {
                IdentifierIsCollection = true,
                ManifestCount = 2,
                HasAlto = true
            },
            new("b30136155", "short MOH report")
            {
                IdentifierIsCollection = false,
                HasAlto = true
            },
            new("b16641097", "Video, no transcript")
            {
                IdentifierIsCollection = false,
                HasAlto = false
            },
            new("b16759230", "Video, expressed as multiple manifestation, but only one manifestation present")
            {
                IdentifierIsCollection = false,
                HasAlto = false
            },
            new("b16675630", "Video, with transcript (2-part multiple manifestation)")
            {
                IdentifierIsCollection = false,
                HasAlto = false,
                HasTranscriptAsDocument = true
            },
            new("b28462270", "PDF only")
            {
                IdentifierIsCollection = false,
                HasAlto = false
            },
            new("b17307922", "Audio, MP3")
            {
                IdentifierIsCollection = false,
                HasAlto = false
            },
            new("b29524404", "Audio Multiple Manifestation (interview with PDF transcript)")
            {
                IdentifierIsCollection = false,
                HasAlto = false,
                HasTranscriptAsDocument = true
            },
            new("b20641151", "A very large archive (5661 images) without access control")
            {
                IdentifierIsCollection = false,
                HasAlto = false
            },
            new("b24963215", "A very large archive (6320 images) with access control")
            {
                IdentifierIsCollection = false,
                HasAlto = false
            },
            new("b24990796", "A work with a very large number of volumes (66 manifestations, 10,136 images)")
            {
                IdentifierIsCollection = true,
                ManifestCount = 67,
                HasAlto = true
            },
            new("b29236927", "A 6 'volume' audio multiple manifestation")
            {
                IdentifierIsCollection = false,
                HasAlto = false
            },
            new("b20298341", "Arabic manuscript, mixed languages")
            {
                IdentifierIsCollection = false, 
                HasAlto = false
            },
            new("b19291449", "Manuscript with structure (TOC)")
            {
                IdentifierIsCollection = false, 
                HasAlto = false
            },
            new("b19192162", "Clickthrough")
            {
                IdentifierIsCollection = false, 
                HasAlto = false
            },
            new("b19974760", "Chemist and Druggist! ??")
            {
                Skip = true, // obvs
                IdentifierIsCollection = false, 
                HasAlto = false
            },
            new("b16673104", "A day at Gebel Moya: ingested mpg, no ingested derivative, TRANSCODE")
            {
                IdentifierIsCollection = false, 
                HasAlto = false
            },
            new("b32718184", "(Malaria): use ingested mp4 derivative 720p as FILE - serve as-is")
            {
                IdentifierIsCollection = false, 
                HasAlto = false
            },
            new("b3223756x", "Duplicity of vision: 2k mp4 ingested, TRANSCODE (later multiple transcodes, 2k, 720?)")
            {
                IdentifierIsCollection = false, 
                HasAlto = false
            },
            new("b1665836x", "Anthelmintics: as above but content-advisory and with PDF")
            {
                IdentifierIsCollection = false, 
                HasAlto = false
            },
            new("b30655729", "(cat), ingested wav, TRANSCODE (single transcode, lo-res)")
            {
                IdentifierIsCollection = false, 
                HasAlto = false
            },
            new("b16677298", "Clinical access condition image (warning, explicit)")
            {
                IdentifierIsCollection = false, 
                HasAlto = false
            },
            new("b11607798",
                    "Clinical image: Hawley Harvey Crippen and Ethel Le Neve. Photograph by Arthur Barrett, 1910 (non-explicit)")
            {
                IdentifierIsCollection = false, 
                HasAlto = false
            },
            new("b16754967", "Clinical video: The chemotherapy of experimental amoebiasis")
            {
                Skip = true, // not currently served
                IdentifierIsCollection = false, 
                HasAlto = false
            },
            new("b1825908x", "Restricted Images: \"Schizophrenia Trust\": minutes, correspondence")
            {
                IdentifierIsCollection = false, 
                HasAlto = false
            },
            new("b18530692", "Restricted Images: 'Lacaille, AD'")
            {
                IdentifierIsCollection = false, 
                HasAlto = false
            },
            new("b29214531", "Restricted Video: Haemorrhage.")
            {
                Skip = true, // Restricted, not served currently
                IdentifierIsCollection = false, 
                HasAlto = false
            }
        };

        foreach (var fixture in List)
        {
            fixture.ManifestShouldBeAfter = minimumDateTime;
        }
    }
}