using Utils;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.AssetDomainRepositories.Mets.Model
{
    public abstract class BaseMetsResource : IMetsResource, IFileBasedResource
    {
        public DdsIdentifier Identifier { get; set; }
        public string Label { get; set; }
        public string Type { get; set; }
        public int? Order { get; set; }
        public ISectionMetadata SectionMetadata { get; set; }
        public ISectionMetadata ParentSectionMetadata { get; set; }
        public IArchiveStorageStoredFileInfo SourceFile { get; set; }
        public bool Partial { get; set; }
        
        protected string GetLabel(ILogicalStructDiv div, ISectionMetadata sectionMetadata)
        {
            string label = null;
            if (sectionMetadata != null)
            {
                if (div.Type == "PeriodicalIssue")
                {
                    var issue = sectionMetadata.GetDisplayTitle();
                    var issueIsUseful = issue.ToAlphanumeric().HasText();
                    if (sectionMetadata.DisplayDate.HasText())
                    {
                        label = sectionMetadata.DisplayDate;
                        if (issueIsUseful)
                        {
                            label += " (issue " + issue + ")";
                        }
                    }
                    else if (issueIsUseful)
                    {
                        label = issue;
                    }
                    else
                    {
                        label = "-";
                    }
                }
                else
                {
                    label = sectionMetadata.GetDisplayTitle();
                }
            }
            if (!label.HasText())
            {
                label = div.Label;
            }
            if (!label.HasText())
            {
                label = div.Type;
            }
            return label;
        }

        /// <summary>
        /// Always returns the package identifier, e.g., b Number
        /// </summary>
        /// <returns></returns>
        public string GetRootId()
        {
            return Identifier.PackageIdentifier;
        }
    }
}
