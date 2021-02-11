using System.Collections.Generic;
using System.Linq;
using IIIF.Presentation;
using IIIF.Presentation.V3;
using Utils;
using Wellcome.Dds.IIIFBuilding;

namespace Wellcome.Dds.Repositories.Presentation.SpecialState
{
    /// <summary>
    /// Types of video / audio
    ///  - Single Manifestation:
    ///    b20997942 - Protect and survive
    ///  - Multiple Manifestation, but only one child manifestation:
    ///    b16756654 - Dying for a smoke
    ///  - Multiple Manifestation, video and PDF transcript:
    ///    b16759138 - If only we'd known.
    ///  - Audio
    ///    b17307922 - Florence Nightingale : greetings to the dear old comrades of Balaclava.
    ///
    ///  - other things:
    ///    - different _download_ permissions. this cannot be expressed in IIIF, it's a viewer-specific UI thing.
    ///    
    ///    Poster images - migrated and new. Currently the poster is not part of the work; ALL
    ///      AV resources are given a poster thumbnail of fixed size. This points at a handler
    ///      in DDS - PosterController - which finds a poster in the bagged work, or returns a
    ///      placeholder. Initially, port this behavior as a placeholderCanvas, which ANY AV manifest gets.
    /// 
    ///  - Clickthrough and Clinical videos
    ///  - MXF videos (ignored)
    ///  - (coming) transcript and video in same manifestation
    ///
    /// Assumption: we always want to end up with one manifest (P3)
    /// - not a collection.
    ///
    /// If we find two vids, that's two canvases.
    /// We can come back to AV collection structure later, if it's needed.
    ///
    /// Regardless of whether the bnumber initially yields a collection or a manifest, we'll:
    ///   - take the first manifest we find (either the resource, or its first child)
    ///   - canvas builder will only make actual canvases for AV, not transcripts; it will note them in AVState
    ///   - we'll join any other manifests we found to this "real" manifest
    ///   - we'll throw away any parent collection, or second manifests, leaving only
    ///     one BuildResult, a Manifest.
    /// 
    /// </summary>
    public class AVState
    {
        public AVState()
        {
            MultipleManifestationMembers = new List<MultipleManifestationMember>();
            Canvases = new List<Canvas>();
        }
        
        public List<MultipleManifestationMember> MultipleManifestationMembers { get; set; }
        public List<Canvas> Canvases { get; set; }
    }



}