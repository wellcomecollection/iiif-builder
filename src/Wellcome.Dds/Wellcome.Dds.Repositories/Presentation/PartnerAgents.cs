using System.Collections.Generic;
using IIIF.Presentation;
using IIIF.Presentation.Content;
using Utils;

namespace Wellcome.Dds.Repositories.Presentation
{
    public static class PartnerAgents
    {
        private static readonly Dictionary<string, Partner> Partners = new Dictionary<string, Partner>
        {
            ["rcpl"] = new Partner
            {
                Label = "Royal College of Physicians",
                HomePage = "https://www.rcplondon.ac.uk/",
                Logo = "rcp-logo.png"
            },
            ["rcpe"] = new Partner
            {
                Label = "Royal College of Physicians Edinburgh",
                HomePage = "https://www.rcpe.ac.uk/",
                Logo = "rcpe-logo.jpg"
            },
            ["rcse"] = new Partner
            {
                // TODO - which RCS(E) is this? Look at the image!
                Label = "Royal College of Surgeons",
                HomePage = "https://www.rcseng.ac.uk/",
                Logo = "rcse-logo.png"
            },
            ["lma"] = new Partner
            {
                Label = "London Metropolitan Archives",
                HomePage =
                    "https://www.cityoflondon.gov.uk/things-to-do/history-and-heritage/london-metropolitan-archives",
                Logo = "LMA-logo_160.gif"
            },
            ["csh"] = new Partner
            {
                Label = "Cold Spring Harbor Laboratory",
                HomePage = "https://library.cshl.edu/archives",
                Logo = "cshl-logo-blue_92w.gif"
            },
            ["bristol"] = new Partner
            {
                Label = "Special Collections of the University of Bristol Library",
                HomePage = "http://www.bristol.ac.uk/library/special-collections/",
                Logo = "bristol-logo.gif"
            },
            ["lshtm"] = new Partner
            {
                Label = "London School of Hygiene and Tropical Medicine",
                HomePage = "https://www.lshtm.ac.uk/",
                Logo = "lshtm-logo.jpg"
            },
            ["leeds"] = new Partner
            {
                Label = "Leeds University Archive",
                HomePage = "https://library.leeds.ac.uk/special-collections/collection/715",
                Logo = "leeds-logo.jpg"
            },
            ["cam"] = new Partner
            {
                Label = "Cambridge University Library",
                HomePage = "https://www.lib.cam.ac.uk/collections/departments/manuscripts-university-archives",
                Logo = "cambridge_logo_160.gif"
            },
            ["gla"] = new Partner
            {
                Label = "University of Glasgow",
                HomePage = "https://www.gla.ac.uk/myglasgow/archives/",
                Logo = "GlasgowUniTransp_160.gif"
            },
            ["kcl"] = new Partner
            {
                Label = "King's College London",
                HomePage = "https://www.kcl.ac.uk/library/collections/archives",
                Logo = "KCL-logo_92w.jpg"
            },
            ["ucl"] = new Partner
            {
                Label = "University College London",
                HomePage = "https://www.ucl.ac.uk/library/special-collections",
                Logo = "ucl0028-pinkbar-160.gif"
            },
            ["borth"] = new Partner
            {
                Label = "Borthwick Institute for Archives",
                HomePage = "https://www.york.ac.uk/borthwick/",
                Logo = "Borthwick_125x108px.png"
            }
        };

        public static Agent GetAgent(string repository, string schemeAndHost)
        {
            Partner partner = null;
            if (repository.HasText())
            {
                repository = repository.ToLowerInvariant();
                if (repository.Contains("royal college"))
                {
                    if (repository.Contains("physicians"))
                    {
                        if (repository.Contains("london"))
                        {
                            partner = Partners["rcpl"];
                        }
                        else if (repository.Contains("edinburgh"))
                        {
                            partner = Partners["rcpe"];
                        }
                    }
                    else if (repository.Contains("surgeons"))
                    {
                        partner = Partners["rcse"];
                    }
                }
                else if (repository.Contains("london metropolitan archives"))
                {
                    partner = Partners["lma"];
                }
                else if (repository.Contains("cold spring harbor"))
                {
                    partner = Partners["csh"];
                }
                else if (repository.Contains("university") && repository.Contains("bristol"))
                {
                    partner = Partners["bristol"];
                }
                else if (repository.Contains("london") && repository.Contains("hygiene") && repository.Contains("tropical medicine"))
                {
                    partner = Partners["lshtm"];
                }
                else if (repository.Contains("university") && repository.Contains("leeds"))
                {
                    partner = Partners["leeds"];
                }
                else if (repository.Contains("cambridge"))
                {
                    partner = Partners["cam"];
                }
                else if (repository.Contains("glasgow"))
                {
                    partner = Partners["gla"];
                }
                else if (repository.Contains("king's college london"))
                {
                    partner = Partners["kcl"];
                }
                else if (repository.Contains("ucl") || repository.Contains("university college london"))
                {
                    partner = Partners["ucl"];
                }
                else if (repository.Contains("borthwick institute"))
                {
                    partner = Partners["borth"];
                }
            }

            return partner != null ? MakeAgent(partner, schemeAndHost) : null;
        }

        private static Agent MakeAgent(Partner partner, string schemeAndHost)
        {
            return new Agent
            {
                Id = $"{partner.HomePage}#",
                Label = Lang.Map(partner.Label),
                Homepage = new List<ExternalResource>
                {
                    new ExternalResource("Text")
                    {
                        Id = partner.HomePage,
                        Label = Lang.Map(partner.Label),
                        Format = "text/html"
                    }
                },
                Logo = new List<Image>
                {
                    new Image
                    {
                        Id = $"{schemeAndHost}/partners/{partner.Logo}",
                        Format = $"image/{partner.Logo.GetFileExtension().ToLowerInvariant()}"
                    }
                }
            };
        }
    }

    /// <summary>
    /// Keep this to the minimum number of fields for maintenance.
    /// </summary>
    class Partner
    {
        public string Logo { get; set; }
        public string HomePage { get; set; }
        public string Label { get; set; }
    }
}