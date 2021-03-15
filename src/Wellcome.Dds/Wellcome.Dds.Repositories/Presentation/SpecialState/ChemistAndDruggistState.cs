using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Amazon.Runtime.Internal.Util;
using Wellcome.Dds.IIIFBuilding;

namespace Wellcome.Dds.Repositories.Presentation.SpecialState
{
    public class ChemistAndDruggistState
    {
        private Regex periodicalDateRegex;
        
        public ChemistAndDruggistState()
        {
            Volumes = new List<ChemistAndDruggistVolume>();
            periodicalDateRegex =  new Regex("(\\d{1,2})([/\\d]*)\\. (\\w+) (\\d{4})");
        }

        public static void ProcessState(MultipleBuildResult buildResults, State state)
        {
            throw new System.NotImplementedException();
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
                    return new DateTime(year, 1, 1);
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
                return DateTime.ParseExact(normalised, "d MMMM yyyy", CultureInfo.InvariantCulture);
            }
            catch (Exception e)
            {
                return DateTime.MinValue;
            }
        }
    }

    public class ChemistAndDruggistIssue
    {
        public ChemistAndDruggistIssue(string identifier)
        {
            Identifier = identifier;
        }
        public string Identifier { get; set; }
        public string Title { get; set; }
        public DateTime NavDate { get; set; }
        public int PartOrder { get; set; }
        public string Number { get; set; }
        public string? Label { get; set; }
        public string Volume { get; set; }
        public int Year { get; set; }
        public string? Month { get; set; }
        public int MonthNum { get; set; }
        public string? DisplayDate { get; set; }
        
        // For CSV-ing
        public string VolumeIdentifier { get; set; }
        public string VolumeDisplayDate { get; set; }
        public DateTime VolumeNavDate { get; set; }
        public string? VolumeLabel { get; set; }
        
        
        
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
        public ChemistAndDruggistVolume(string identifier)
        {
            Identifier = identifier;
            Issues = new List<ChemistAndDruggistIssue>();
        }

        public string Identifier { get; set; }
        public string DisplayDate { get; set; }
        public DateTime NavDate { get; set; }
        public string? Label { get; set; }
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