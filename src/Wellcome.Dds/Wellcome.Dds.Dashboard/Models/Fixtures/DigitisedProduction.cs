using System;

namespace Wellcome.Dds.Dashboard.Models.Fixtures;

public class DigitisedProduction
{
    public readonly (string, string)[] Identifiers = {
        ("b2178081x", "2 smallish volumes"),
        ("b22454408", "short text - wl.org origin (other bucket)"),
        ("b2043067x", "2 images no text"),
        ("b24923333", "2 x 550 page MM"),
        ("b30136155", "short MOH report"),
        ("b16641097", "Video, no transcript"),
        ("b16759230", "Video, expressed as multiple manifestation, but only one manifestation present"),
        ("b16675630", "Video, with transcript (2-part multiple manifestation)"),
        ("b28462270", "PDF only (born digital but currently in same workflow)"),
        ("b17307922", "Audio, MP3"),
        ("b29524404", "Audio Multiple Manifestation (interview with PDF transcript)"),
        ("b20641151", "A very large archive (5661 images) without access control"),
        ("b24963215", "A very large archive (6320 images) with access control"),
        ("b24990796", "A work with a very large number of volumes (66 manifestations, 10,136 images)"),
        ("b29236927", "A 6 'volume' audio multiple manifestation"),
        ("b20298341", "Arabic manuscript, mixed languages"),
        ("b19291449", "Manuscript with structure (TOC)"),
        ("b19192162", "Clickthrough"),
        ("b19974760", "Chemist and Druggist! 😱"),
        ("b16673104", "A day at Gebel Moya: ingested mpg, no ingested derivative, TRANSCODE"),
        ("b32718184", "(Malaria): use ingested mp4 derivative 720p as FILE - serve as-is"),
        ("b3223756x", "Duplicity of vision: 2k mp4 ingested, TRANSCODE (later multiple transcodes, 2k, 720?)"),
        ("b1665836x", "Anthelmintics: as above but content-advisory and with PDF"),
        ("b30655729", "(cat), ingested wav, TRANSCODE (single transcode, lo-res)"),
        ("b16677298", "Clinical access condition image (warning, explicit)"),
        ("b11607798", "Clinical image: Hawley Harvey Crippen and Ethel Le Neve. Photograph by Arthur Barrett, 1910 (non-explicit)"),
        ("b16754967", "Clinical video: The chemotherapy of experimental amoebiasis"),
        ("b1825908x", "Restricted Images: \"Schizophrenia Trust\": minutes, correspondence"),
        ("b18530692", "Restricted Images: 'Lacaille, AD'"),
        ("b29214531", "Restricted Video: Haemorrhage.")
    };
}