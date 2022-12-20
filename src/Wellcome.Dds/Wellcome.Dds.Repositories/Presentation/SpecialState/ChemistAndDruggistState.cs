using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Constants;
using IIIF.Presentation.V3.Strings;
using Wellcome.Dds.IIIFBuilding;
using Version = IIIF.Presentation.Version;

namespace Wellcome.Dds.Repositories.Presentation.SpecialState
{
    public class ChemistAndDruggistState
    {
        private readonly Regex periodicalDateRegex;
        private UriPatterns uriPatterns;
        
        public ChemistAndDruggistState(UriPatterns uriPatterns)
        {
            this.uriPatterns = uriPatterns;
            Volumes = new List<ChemistAndDruggistVolume>();
            periodicalDateRegex =  new Regex("(\\d{1,2})([/\\d]*)\\. (\\w+) (\\d{4})");
        }

        public void ProcessState(MultipleBuildResult buildResults)
        {
            var pseudoVolumeBuildResults = new List<BuildResult>();
            Volumes.Sort((volume1, volume2) => DateTime.Compare(volume1.NavDate, volume2.NavDate));
            foreach (var volume in Volumes)
            {
                // Ths should not produce a different output
                volume.Issues.Sort((issue1, issue2) => DateTime.Compare(issue1.NavDate, issue2.NavDate));
            }

            var topCollection = buildResults.First().IIIFResource as Collection;
            // ignore any that have accumulated in the build.
            topCollection.Items = new List<ICollectionItem>();
            topCollection.Behavior = new List<string> {Behavior.MultiPart};

            foreach (var volume in Volumes)
            {
                var volumeCollection = new Collection
                {
                    Id = uriPatterns.CollectionForWork(volume.Identifier),
                    Label = Lang.Map(volume.Label!),
                    Behavior = new List<string>{Behavior.MultiPart},
                    Items = new List<ICollectionItem>()
                };
                topCollection.Items.Add(volumeCollection);
                // Add this intermediate collection to the BuildResults so it will also be constructed as IIIF
                // At this point it does not have its @context, but we don't want to add that until
                // all of the existing IIIF has been serialised out and saved; we don't want the @context
                // appearing on nested resources.
                var pseudoCollectionBuildResult = new BuildResult(volume.Identifier, Version.V3)
                {
                    IIIFResource = volumeCollection
                };
                pseudoVolumeBuildResults.Add(pseudoCollectionBuildResult);
                foreach (var issue in volume.Issues)
                {
                    var issueManifest = new Manifest
                    {
                        Id = uriPatterns.Manifest(issue.Identifier),
                        Label = Lang.Map(issue.Label!),
                        NavDate = issue.NavDate.ToString("O"),
                        Metadata = new List<LabelValuePair>()
                    };
                    volumeCollection.Items.Add(issueManifest);
                    volumeCollection.NavDate ??= issueManifest.NavDate;
                    issueManifest.Metadata.AddNonlang("Volume", issue.Volume);
                    issueManifest.Metadata.AddNonlang("Year", issue.Year.ToString());
                    issueManifest.Metadata.AddEnglish("Month", issue.Month!);
                    issueManifest.Metadata.AddEnglish("DisplayDate", issue.DisplayDate!);

                    var builtManifest = buildResults[issue.Identifier]?.IIIFResource as Manifest;
                    if (builtManifest == null)
                    {
                        // This should not happen unless we are constraining C&D to a limited set
                        continue;
                    }
                    builtManifest.Label = Lang.Map("en", "The chemist and druggist, " + issue.Label);
                    var parentVolume = new Collection
                    {
                        Id = uriPatterns.CollectionForWork(volume.Identifier),
                        Label = Lang.Map(volume.Label),
                        Behavior = new List<string> {Behavior.MultiPart},
                        PartOf = new List<ResourceBase>
                        {
                            new Collection
                            {
                                Id = uriPatterns.CollectionForWork(buildResults.Identifier),
                                Label = topCollection.Label,
                                Behavior = new List<string> {Behavior.MultiPart}
                            }
                        }
                    };
                    builtManifest.PartOf = new List<ResourceBase> {parentVolume};
                }
            }

            foreach (var buildResult in pseudoVolumeBuildResults)
            {
                buildResults.Add(buildResult);
            }
        }

        public List<ChemistAndDruggistVolume> Volumes { get; set; }
        
        public DateTime GetNavDate(string modsDataOriginDateDisplay)
        {
            return GetPeriodicalPublicationDate(modsDataOriginDateDisplay);
        }

        private DateTime GetPeriodicalPublicationDate(string modsDate)
        {
            modsDate = modsDate.Trim();
            if (modsDate.Length == 4)
            {
                if (int.TryParse(modsDate, out var year))
                {
                    return new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                }
            }
            // observed date formats:
            // "8. December 1973"  -- note the . after the day of month
            // "22/29. December 1973"  -- note the double date

            // first normalise to d MMMM yyyy
            try
            {
                var m = periodicalDateRegex.Match(modsDate);
                var normalised = $"{m.Groups[1].Value} {m.Groups[3].Value} {m.Groups[4].Value}";
                var dt =  DateTime.ParseExact(normalised, "d MMMM yyyy", 
                    CultureInfo.InvariantCulture, 
                    DateTimeStyles.AdjustToUniversal)
                    .ToUniversalTime()
                    .AddHours(12); // set to midday
                return dt;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }
    }

    public class ChemistAndDruggistIssue
    {
        #nullable disable
        public ChemistAndDruggistIssue(string identifier)
        {
            Identifier = identifier;
        }
        public string Identifier { get; set; }
        public string Title { get; set; }
        public DateTime NavDate { get; set; }
        public int PartOrder { get; set; }
        public string Number { get; set; }
        public string Label { get; set; }
        public string Volume { get; set; }
        public int Year { get; set; }
        public string Month { get; set; }
        public int MonthNum { get; set; }
        public string DisplayDate { get; set; }
        
        // For CSV-ing
        public string VolumeIdentifier { get; set; }
        public string VolumeDisplayDate { get; set; }
        public DateTime VolumeNavDate { get; set; }
        public string VolumeLabel { get; set; }
        
        
        
        public override string ToString()
        {
            return "    Issue identifier: " + Identifier + Environment.NewLine
                   + "    Title: " + Title + Environment.NewLine
                   + "    Label: " + Label + Environment.NewLine
                   + "    Volume: " + Volume + Environment.NewLine
                   + "    NavDate: " + NavDate + Environment.NewLine
                   + "    PartOrder: " + PartOrder + Environment.NewLine
                   + "    Number: " + Number + Environment.NewLine
                   + "    DisplayDate: " + DisplayDate + Environment.NewLine
                   + "    Year: " + Year + Environment.NewLine
                   + "    Month: " + Month + Environment.NewLine
                   + "    MonthNum: " + MonthNum + Environment.NewLine;
        }
    }

    public class ChemistAndDruggistVolume
    {
        #nullable disable
        public ChemistAndDruggistVolume(string identifier)
        {
            Identifier = identifier;
            Issues = new List<ChemistAndDruggistIssue>();
        }

        public string Identifier { get; set; }
        public string DisplayDate { get; set; }
        public DateTime NavDate { get; set; }
        public string Label { get; set; }
        public string Volume { get; set; }
        public List<ChemistAndDruggistIssue> Issues { get; set; }

        public override string ToString()
        {
            return "Volume identifier: " + Identifier + Environment.NewLine
                   + "Label: " + Label + Environment.NewLine
                   + "Volume: " + Volume + Environment.NewLine
                   + "NavDate: " + NavDate + Environment.NewLine;
        }
    }
}