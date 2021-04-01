using System.Collections.Generic;
using Utils;

namespace Wellcome.Dds.Repositories.Presentation.LicencesAndRights.LegacyConfig
{

    public class PlayerConfigProvider
    {
        private static readonly PlayerConfigProvider Instance = new PlayerConfigProvider();
        private readonly PlayerConfig baseConfig;
        private readonly PlayerConfig mohConfig;


        public static PlayerConfig BaseConfig
        {
            get { return Instance.baseConfig; }
        }

        public static PlayerConfig MoHConfig
        {
            get { return Instance.mohConfig; }
        }

        static PlayerConfigProvider()
        {
        }

        private PlayerConfigProvider()
        {
            // for the canonical player
            baseConfig = new PlayerConfig
            {
                Options = new OptionSet
                {
                    SaveToLightboxEnabled = true,
                    PreloadMoreInfo = true,
                    SeeAlsoEnabled = true,
                    SingleSignOn = false // StringUtils.GetBoolFromAppSetting("CAS-Enabled", false)
                },
                Modules = new ModuleSet
                {
                    HelpDialogue = new HelpDialogue { Content = GetHelpDialogue() },
                    ConditionsDialogue = new ConditionsDialogue { Content = GetConditions() }
                }
            };

            // for the player embedded in MoH
            mohConfig = new PlayerConfig
            {
                Options = new OptionSet
                {
                    SaveToLightboxEnabled = false,
                    PreloadMoreInfo = true,
                    SeeAlsoEnabled = false,
                    SingleSignOn = false // StringUtils.GetBoolFromAppSetting("CAS-Enabled", false)
                },
                Modules = new ModuleSet
                {
                    HelpDialogue = new HelpDialogue { Content = GetHelpDialogue() },
                    ConditionsDialogue = new ConditionsDialogue { Content = GetConditions() },
                    TreeViewLeftPanel = new TreeViewLeftPanel { Options = new TreeViewLeftPanelOptions { PanelOpen = false } },
                    SeadragonCenterPanel = new SeadragonCenterPanel { Options = new SeadragonCenterPanelOptions { TitleEnabled = false } },
                    PagingHeaderPanel = new PagingHeaderPanel { Options = new PagingHeaderPanelOptions { HelpEnabled = false, ModeOptionsEnabled = false } },
                    SearchFooterPanel = new SearchFooterPanel { Options = new SearchFooterPanelOptions { MinimiseButtons = true } }
                }
            };
        }

        private SimpleDialogue GetHelpDialogue()
        {
            return new SimpleDialogue
            {
                Title = "Help",
                Text = "(No longer used)"
            };
        }

        private Dictionary<string, string> GetConditions()
        {
            var dict = new Dictionary<string, string>();
            dict["title"] = "Conditions of use";

            // The following DZ code should be mapped to licenses with URIs
            dict["B"] = "You have permission to reuse this work under an Attribution Non-commercial license.<br/><br/>Non-commercial use includes private study, academic research, teaching, and other activities that are not primarily intended for, or directed towards, commercial advantage or private monetary compensation. <br/><br/>Image source should be attributed as specified in the full catalogue record. If no source is given the image should be attributed to Wellcome Collection.<br/><br/>Where applicable the terms of this license have been agreed with the copyright holder(s).";
            dict["C"] = "You have permission to reuse this image under an Attribution Non-commercial, No derivatives license.<br/><br/>Non-commercial use includes private study, academic research, teaching, and other activities that are not primarily intended for, or directed towards, commercial advantage or private monetary compensation. <br/><br/>Image source should be attributed as specified in the full catalogue record. If no source is given the image should be attributed to Wellcome Collection.<br/><br/>Altering, adapting, modifying or translating the work is prohibited.<br/><br/>Where applicable the terms of this license have been agreed with the copyright holder(s).";
            dict["D"] = "You have permission to make copies of this work for private use only.<br/><br/>Sharing (issuing, performing or communicating) copies of the work in whole or in part (in print or electronic form) with others is prohibited.<br/><br/>Where applicable the terms of this license have been agreed with the copyright holder(s).";
            dict["E"] = "You have permission to reuse this work under an Attribution, Non-commercial license, however only extracts from this work (single images, or parts of images) may be downloaded.<br/><br/>Non-commercial use includes private study, academic research, teaching, and other activities that are not primarily intended for, or directed towards, commercial advantage or private monetary compensation.<br/><br/>Image source should be attributed as specified in the full catalogue record. If no source is given the image should be attributed to Wellcome Collection.<br/><br/>Where applicable the terms of this license have been agreed with the copyright holder(s).";
            dict["F"] = "You have permission to reuse this work under an Attribution, Non-commercial, no derivatives license, however only extracts from this work (single images, or parts of images) may be downloaded.<br/><br/>Non-commercial use includes private study, academic research, teaching, and other activities that are not primarily intended for, or directed towards, commercial advantage or private monetary compensation. <br/><br/>Image source should be attributed as specified in the full catalogue record. If no source is given the image should be attributed to Wellcome Collection.<br/><br/>Only extracts from this work (single images, or parts of images) may be downloaded.<br/><br/>Altering, adapting, modifying or translating the work is prohibited.<br/><br/>Where applicable the terms of this license have been agreed with the copyright holder(s).";
            dict["G"] = "You have permission to make copies of this work for private use only, however only extracts from this work (single images, or parts of images) may be downloaded. <br/><br/>Sharing (issuing, performing or communicating) copies of the work in whole or in part (in print or electronic form) with others is prohibited.<br/><br/>Where applicable the terms of this license have been agreed with the copyright holder(s).";
            dict["J"] = "You have permission to make copies of this work under an Attribution, Non-commercial license. Additionally, you must not misuse any personal or sensitive data. Where such data exists, it must be anonymised before the image is reused.<br/><br/>Non-commercial use includes private study, academic research, teaching, and other activities that are not primarily intended for, or directed towards, commercial advantage or private monetary compensation.<br/><br/>Image source should be attributed as specified in the full catalogue record. If no source is given the image should be attributed to Wellcome Collection.";
            dict["N"] = "You have permission to reuse this work under an Attribution, Non-commercial license.<br/><br/>Non-commercial use includes private study, academic research, teaching, and other activities that are not primarily intended for, or directed towards, commercial advantage or private monetary compensation. <br/><br/>Image source should be attributed as specified in the full catalogue record. If no source is given the image should be attributed to the Wellcome Collection.<br/><br/>Where applicable the terms of this license have been agreed with the copyright holder(s).";
            dict["O"] = "You have permission to reuse this work under an Attribution, Non-commercial, No derivatives license.<br/><br/>Non-commercial use includes private study, academic research, teaching, and other activities that are not primarily intended for, or directed towards, commercial advantage or private monetary compensation. <br/><br/>Image source should be attributed as specified in the full catalogue record. If no source is given the image should be attributed to Wellcome Collection.<br/><br/>Altering, adapting, modifying or translating the work is prohibited.<br/><br/>The terms of this license have been agreed with the copyright holder(s).";
            dict["P"] = "You have permission to make copies of this work for private use only.<br/><br/>Sharing (issuing, performing or communicating) copies of the work in whole or in part (in print or electronic form) with others is prohibited.<br/><br/>Where applicable the terms of this license have been agreed with the copyright holder(s).";
            dict["Q"] = "You have permission to make copies of this work under an Attribution, Non-commercial license. Additionally, you must not misuse any personal or sensitive data. Where such data exists, it must be anonymised before the image is reused.<br/><br/>Non-commercial use includes private study, academic research, teaching, and other activities that are not primarily intended for, or directed towards, commercial advantage or private monetary compensation.<br/><br/>Image source should be attributed as specified in the full catalogue record. If no source is given the image should be attributed to Wellcome Collection.";

            // new player codes; these all have URIs assigned in Wellcome.Dds.LinkedData.LodProviders.PackageTripleProvider::GetLicenseNode()
            // where possible the old code has been mapped to the new one
            dict["PDM"] = "This work has been identified as being free of known restrictions under copyright law, including all related and neighbouring rights and is being made available under the <a target=\"_top\" href=\"http://creativecommons.org/publicdomain/mark/1.0/\">Creative Commons, Public Domain Mark</a>.<br/><br/>You can copy, modify, distribute and perform the work, even for commercial purposes, without asking permission.";
            dict["S"] = dict["PDM"];

            dict["CC-BY"] = "You have permission to make copies of this work under a <a target=\"_top\" href=\"http://creativecommons.org/licenses/by/4.0/\">Creative Commons, Attribution license</a>.<br/><br/>This licence permits unrestricted use, distribution, and reproduction in any medium, provided the original author and source are credited. See the <a target=\"_top\" href=\"http://creativecommons.org/licenses/by/4.0/legalcode\">Legal Code</a> for further information.<br/><br/>Image source should be attributed as specified in the full catalogue record. If no source is given the image should be attributed to Wellcome Collection.";
            dict["R"] = dict["CC-BY"];

            dict["CC-BY-NC"] = "You have permission to make copies of this work under a <a target=\"_top\" href=\"http://creativecommons.org/licenses/by-nc/4.0/\">Creative Commons, Attribution, Non-commercial license</a>.<br/><br/>Non-commercial use includes private study, academic research, teaching, and other activities that are not primarily intended for, or directed towards, commercial advantage or private monetary compensation. See the <a target=\"_top\" href=\"http://creativecommons.org/licenses/by-nc/4.0/legalcode\">Legal Code</a> for further information.<br/><br/>Image source should be attributed as specified in the full catalogue record. If no source is given the image should be attributed to Wellcome Collection.";
            dict["A"] = dict["CC-BY-NC"];
            dict["L"] = dict["CC-BY-NC"];

            dict["CC-BY-NC-ND"] = "You have permission to make copies of this work under a <a target=\"_top\" href=\"http://creativecommons.org/licenses/by-nc-nd/4.0/\">Creative Commons, Attribution, Non-commercial, No-derivatives license</a>. <br/><br/> Non-commercial use includes private study, academic research, teaching, and other activities that are not primarily intended for, or directed towards, commercial advantage or private monetary compensation. See the <a target=\"_top\" href=\"http://creativecommons.org/licenses/by-nc-nd/4.0/legalcode\">Legal Code</a> for further information.<br/><br/>Image source should be attributed as specified in the full catalogue record. If no source is given the image should be attributed to Wellcome Collection.<br/><br/> Altering, adapting, modifying or translating the work is prohibited.";
            dict["K"] = dict["CC-BY-NC-ND"];
            dict["M"] = dict["CC-BY-NC-ND"];

            // the following new codes have no
            dict["CC-0"] = "You have permission to make copies of this work under a <a target=\"_top\" href=\"https://creativecommons.org/publicdomain/zero/1.0/\">Creative Commons Public Domain Dedication</a>.<br/><br/>This license permits unrestricted use, distribution, and reproduction in any medium. See the <a target=\"_top\" href=\"https://creativecommons.org/publicdomain/zero/1.0/legalcode\">Legal Code</a> for further information.";
            dict["CC-BY-ND"] = "You have permission to make copies of this work under a <a target=\"_top\" href=\"http://creativecommons.org/licenses/by-nd/4.0/\">Creative Commons, Attribution, No-derivatives license</a>. <br/><br/> This licence allows you to copy and redistribute the material in any medium or format for any purpose, even commercially. Image source should be attributed as specified in the full catalogue record. If no source is given the image should be attributed to Wellcome Collection.<br/><br/> Altering, adapting, modifying or translating the work is prohibited.";
            dict["CC-BY-SA"] = "You have permission to make copies of this work under a <a target=\"_top\" href=\"http://creativecommons.org/licenses/by-sa/4.0/\">Creative Commons, Attribution, Share-Alike license</a>.<br/><br/>This licence permits unrestricted use, distribution, and reproduction in any medium, provided the original author and source are credited. If you remix, transform, or build upon the material, you must distribute your contributions under the same license as the original. See the <a target=\"_top\" href=\"http://creativecommons.org/licenses/by-sa/4.0/legalcode\">Legal Code</a> for further information.<br/><br/>Image source should be attributed as specified in the full catalogue record. If no source is given the image should be attributed to Wellcome Collection.";
            dict["CC-BY-NC-SA"] = "You have permission to make copies of this work under a <a target=\"_top\" href=\"http://creativecommons.org/licenses/by-nc-sa/4.0/\">Creative Commons, Attribution, Non-commercial, Share-Alike license</a>.<br/><br/>This licence allows you to copy and redistribute the material in any medium or format and remix, transform, and build upon the material.  If you remix, transform, or build upon the material, you must distribute your contributions under the same license as the original. You may not use the material for commerical purposes. See the <a target=\"_top\" href=\"http://creativecommons.org/licenses/by-nc-sa/4.0/legalcode\">Legal Code</a> for further information.<br/><br/>Image source should be attributed as specified in the full catalogue record. If no source is given the image should be attributed to Wellcome Collection.";
            dict["OGL"] = "You have permission to make copies of this work under an <a target=\"_top\" href=\"http://www.nationalarchives.gov.uk/doc/open-government-licence/version/2/\">Open Government license</a>.<br/><br/>This licence permits unrestricted use, distribution, and reproduction in any medium, provided the original author and source are credited. <br/><br/>Image source should be attributed as specified in the full catalogue record. If no source is given the image should be attributed to Wellcome Collection.";
            dict["OPL"] = "You have permission to make copies of this work under an <a target=\"_top\" href=\"http://www.parliament.uk/site-information/copyright/open-parliament-licence/\">Open Parliament license</a>.<br/><br/>This licence permits unrestricted use, distribution, and reproduction in any medium, provided the original author and source are credited. <br/><br/>Image source should be attributed as specified in the full catalogue record. If no source is given the image should be attributed to Wellcome Collection.";
            dict["ARR"] = "The work has been made available under an \"all rights reserved licence\". See the full catalogue record for further information about what rights you have to make copies of this work.";
            dict[Constants.CopyrightNotClearedCondition] = Constants.CopyrightNotClearedStatement;
            dict[Constants.InCopyrightCondition] = Constants.InCopyrightStatement;
            return dict;
        }
    }
}