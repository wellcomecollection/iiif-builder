using Utils;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.AssetDomainRepositories.Mets.Model
{
    public abstract class BaseMetsResource : IMetsResource, IFileBasedResource
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string Type { get; set; }
        public int? Order { get; set; }
        public IModsData ModsData { get; set; }
        public IModsData ParentModsData { get; set; }
        public IStoredFileInfo SourceFile { get; set; }
        public bool Partial { get; set; }
        
        protected string GetLabel(ILogicalStructDiv div, IModsData mods)
        {
            string label = null;
            if (mods != null)
            {
                if (div.Type == "PeriodicalIssue")
                {
                    var issue = mods.GetDisplayTitle();
                    var issueIsUseful = issue.ToAlphanumeric().HasText();
                    if (mods.OriginDateDisplay.HasText())
                    {
                        label = mods.OriginDateDisplay;
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
                    label = mods.GetDisplayTitle();
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
        /// Always returns the b Number
        /// </summary>
        /// <returns></returns>
        public string GetRootId()
        {
            return Id.SplitByDelimiterIntoArray('_')[0];
        }
    }
}
