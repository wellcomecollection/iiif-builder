using System;
using System.Collections.Generic;
using Wellcome.Dds.IIIFBuilding;

namespace Wellcome.Dds.Repositories.Presentation.SpecialState
{
    public class ChemistAndDruggistState
    {
        public ChemistAndDruggistState()
        {
            Volumes = new List<ChemistAndDruggistVolume>();
        }

        public static void ProcessState(MultipleBuildResult buildResults, State state)
        {
            throw new System.NotImplementedException();
        }

        public List<ChemistAndDruggistVolume> Volumes { get; set; }
        
        public static DateTime GetNavDate(string modsDataOriginDateDisplay)
        {
            throw new NotImplementedException();
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