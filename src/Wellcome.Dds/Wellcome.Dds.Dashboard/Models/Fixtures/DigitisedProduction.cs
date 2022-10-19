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
        ("b19974760", "Chemist and Druggist! 😱")
    };
}    
