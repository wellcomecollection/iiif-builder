using System.Collections.Generic;

namespace Wellcome.Dds.Catalogue
{
    public static class DigitalCollectionsMap
    {
        private static readonly Dictionary<string, string> Lookups = new()
        {
            {"digukmhl", "Digitised by the Internet Archive for the UK-MHL Project (all content)"},
            {"digmoh", "All MOH Reports"},
            {"dig19th", "19th Century books from Wellcome collections"},
            {"diggenetics", "Genetics books and archives"},
            {"digrcs", "Royal College of Surgeons books books digitised as part of the UK-MHL project"},
            {"digmhl", "Records brought in by Joao from the MHL."},
            {"digasylum", "Asylum and Beyond"},
            {"digrcpe", "Royal College of Physicians Edinburgh books books digitised as part of the UK-MHL project"},
            {"digephemera", "Ephemera digitisation"},
            {"digglasgow", "Glasgow University books digitised as part of UK-MHL"},
            {"digwhmm", "Wellcome Historical Medical Museum archives"},
            {"digramc", "RAMC (books and archives)"},
            {"digrcpl", "Royal College of Physicians London books digitised as part of the UK-MHL project"},
            {"digleeds", "Leeds University books digitised as part of UK-MHL project"},
            {"digaids", "AIDS posters"},
            {"digucl", "UCL books digitised as part of the UK-MHL project"},
            {"diglshtm", "LSHTM books digitised as part of UK-MHL project. "},
            {"dig20th", "20th century out of copyright books"},
            {"digkings", "Kings College London books digitised as part of UK-MHL project"},
            {"digicon", "General iconographic digitisation"},
            {"diggardiner", "James Gardiner Collection photographs"},
            {"digfilm", "Wellcome Film"},
            {"digbristol", "Bristol University books digitised as part of UK-MHL"},
            {"digmuybridge", "Eadweard Muybridge Animal Locomotion prints"},
            {"digproquest", "Pre-1700 books digitised by Proquest"},
            {"digmbishop", "Mary Bishop paintings from the Adamson Collection"},
            {"digearly", "16th and 17th century British books digitised by the Internet Archive"},
            {"digarabic", "Arabic manuscripts "},
            {"digrecipe", "Recipe books (manuscripts)"},
            {"digklein", "Melanie Klein archive"},
            {"digwtrust", "Wellcome Trust-related PDFs"},
            {"digrr", "Reading Room project"},
            {"digwms", "Medieval manuscripts"},
            {"digwellpub", "Wellcome Publications"},
            {"digadhoc", "Adhoc digitisation"},
            {"digprojectx", "Material digitised for ProjectX not used in Pathways."},
            {"digpathways", "Digitised material used in Pathways. The suffix indicates the particular Pathway."},
            {"digpbd", "Non-Wellcome Trust related PDFs"},
            {"digsexology", "Sexology mini-theme"},
            {"digancestry", "Ancestry"},
            {"digforensics", "Forensics mini-theme"},
            {"digbiomed", "Biomedical images (for Wellcome Images Awards)"},
            {"digflorence", "Florence Nightingale books brought in for the Internet Archive (at the request of Simon Chaplin)"},
            {"digbeard", "Items on the theme of facial hair digitised for Movember"},
            {"digelectric", "Items digitised for Electricity exhibition."},
            {"digobp", "PDF e-books from Open Book Publishers"},
            {"digmalay", "Malay manuscripts and related material."},
            {"digbedlam", "Items digitised for the 'Bedlam' exhibition."},
            {"digthomson", "John Thomson Collection"},
            {"digmemory", "Items digitised for 'Memory Movement, Memory Objects' exhibition."},
            {"digtoft", "Mary Toft (pre-Player)"},
            {"digpaintings", "Oil paintings digitisation project"},
            {"digaudio", "Sound recordings"},
            {"digfugitive", "Fugitive sheets (pre-Player)"}
        };

        public static string GetFriendlyName(string collectionCode)
        {
            return Lookups.ContainsKey(collectionCode) ? Lookups[collectionCode] : null;
        }
    }
}