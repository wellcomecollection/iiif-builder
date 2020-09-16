using System;
using System.Collections.Generic;
using System.Linq;
using IIIF.Presentation;
using IIIF.Presentation.Constants;
using Utils;
using Utils.Sort;

namespace Wellcome.Dds.Repositories.Presentation
{
    /// <summary>
    /// Utility for producing a more human-friendly structure (natural reading order)
    /// </summary>
    public class ManifestStructureHelper
    {
        private readonly Dictionary<string, string> sectionTypeMappings;
        private const string FrontCover = "Front Cover";
        private const string BackCover = "Back Cover";
        private const string TitlePage = "Title Page";
        private const string TableOfContents = "Table of Contents";
        private const string PartOfWork = "Part of Work";

        public ManifestStructureHelper()
        {
            // Used to give ranges friendlier labels than Wellcome publish in METS
            sectionTypeMappings = new Dictionary<string, string>
            {
                {"CoverFrontOutside", FrontCover},
                {"CoverBackOutside", BackCover},
                {"TitlePage", TitlePage},
                {"TableOfContents", TableOfContents},
                {"PartOfWork", PartOfWork}
            };
        }

        public string GetHumanFriendlySectionLabel(string metsLabel)
        {
            if (sectionTypeMappings.ContainsKey(metsLabel))
            {
                return sectionTypeMappings[metsLabel];
            }
            return metsLabel; // not mapped, return what was given
        }

        /// <summary>
        /// Move the back cover to the end of the sequence
        /// Identify a suitable anchor point that MUST be recto
        /// If this isn't recto in the paging sequence, try to re-flow pages before it
        /// 
        /// ONLY manifests with one sequence supported for now.
        /// </summary>
        /// <param name="manifest"></param>
        public void ImprovePagingSequence(Manifest manifest)
        {
            // Can we do anything with this manifest?
            if (!manifest.Items.HasItems()) return;
            if (!manifest.Behavior.HasItems()) return;
            if (!manifest.Behavior.Contains(Behavior.Paged)) return;
            
            // We're looking at a *paged* sequence. This behavior must have been set before we start with the re-arranging.
            
            string knownRectoCanvasId = null;
            // Allow for more than one back cover range, and more than one back cover canvas in each range!
            // Assume that within the sections the back covers are in reading order in the METS.
            if (manifest.Structures != null && manifest.Structures.Count > 0)
            {
                var backCoverRanges =
                    manifest.Structures.Where(r => SafeLabel(r) == BackCover).ToList();
                backCoverRanges.Reverse();
                int requiredRangePos = manifest.Structures.Count - 1;
                int requiredCanvasPos = manifest.Items.Count - 1;
                foreach (var range in backCoverRanges)
                {
                    int currentRangePos = manifest.Structures.FindIndex(r => r.Id == range.Id);
                    if (currentRangePos != requiredRangePos)
                    {
                        manifest.Structures.ShiftElement(currentRangePos, requiredRangePos);
                    }
                    foreach (var structuralLocation in range.Items.AsEnumerable().Reverse())
                    {
                        var canvas = structuralLocation as Canvas;
                        if (canvas == null)
                        {
                            continue;
                        }
                        int currentCanvasPos = manifest.Items.FindIndex(c => c.Id == canvas.Id);
                        if (currentCanvasPos != requiredCanvasPos)
                        {
                            manifest.Items.ShiftElement(currentCanvasPos, requiredCanvasPos);
                        }
                        requiredCanvasPos--;
                    }
                    requiredRangePos--;
                }
                var titlePageRange = manifest.Structures.FirstOrDefault(r => SafeLabel(r) == TitlePage);
                if (titlePageRange != null && titlePageRange.Items.HasItems())
                {
                    knownRectoCanvasId = titlePageRange.Items.Cast<Canvas>().First().Id;
                }
            }
            
            
            if (string.IsNullOrWhiteSpace(knownRectoCanvasId))
            {
                // couldn't find a title page canvas, try to find one by label "1r"
                var recto1 = manifest.Items.FirstOrDefault(c => SafeLabel(c) == "1r");
                if (recto1 != null)
                {
                    knownRectoCanvasId = recto1.Id;
                }
            }
            if (string.IsNullOrWhiteSpace(knownRectoCanvasId))
            {
                // can't do anything else
                return;
            }
            
            // danger - this doesn't take into account canvases that are already non-paged!
            // Wellcome don't have any yet, but this will break when they do. 
            // Also this utility should produce the same output if you feed a manifest through it
            // multiple times.
            //var recto1Pos = FindIndexById(sequence.Canvases, knownRectoCanvasId);
            
            // you can't use recto1Pos % 2 == 0, you have to use the position within paged "pages" only
            
            var pagedSeq = manifest.Items.Where(c => !IsNonPaged(c)).ToList();
            int recto1PosWithinPaging = pagedSeq.FindIndex(c => c.Id == knownRectoCanvasId);
            
            if (recto1PosWithinPaging == -1 || recto1PosWithinPaging % 2 == 0)
            {
                // first recto is either not found or already at the correct offset (even numbered)
                return;
            }
            
            // Right, our first recto is in the wrong place. What do we do now?
            // We've already shifted the back cover, if that was in the wrong place.
            // We can't safely MOVE a page between the cover and the title page, because we don't know what they are.
            
            // The safest bet is to mark one of them as non-paged, so that a viewer will offset the paging sequence.
            // In the absence of other info this will be the one preceding the first recto.
            if (recto1PosWithinPaging > 0)
            {
                // Later this data could be improved, if we have paging information in METS.
                pagedSeq[recto1PosWithinPaging - 1].Behavior = new List<string> {Behavior.NonPaged};
            }
        }

        private int FindIndexById(List<StructureBase> list, string id)
        {
            return list.FindIndex(sb => sb.Id == id);
            // This was the former array-based code:
            // for (int index = 0; index < list.Count; index++)
            // {
            //     if (list[index].Id == id)
            //     {
            //         return index;
            //     }
            // }
            // return -1;
        }

        private string SafeLabel(StructureBase sb)
        {
            return sb?.Label == null ? string.Empty : sb.Label.ToString().Trim();
        }

        private bool IsNonPaged(Canvas c)
        {
            return c.Behavior != null && c.Behavior.Contains(Behavior.NonPaged);
        }
    }
}
