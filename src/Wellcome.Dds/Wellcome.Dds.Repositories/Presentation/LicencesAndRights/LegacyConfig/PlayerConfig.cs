using System.Collections.Generic;

namespace Wellcome.Dds.Repositories.Presentation.LicencesAndRights.LegacyConfig
{
    /// <summary>
    /// This class can be extended to match the player's Javascript configuration
    /// </summary>
    public class PlayerConfig
    {
        public ModuleSet Modules { get; set; }
        public OptionSet Options { get; set; }
    }

    public class OptionSet
    {
        public bool SaveToLightboxEnabled { get; set; }
        public bool PreloadMoreInfo { get; set; }
        public bool SeeAlsoEnabled { get; set; }
        public bool SingleSignOn { get; set; }
    }

    public class ModuleSet
    {
        public HelpDialogue HelpDialogue { get; set; }
        public ConditionsDialogue ConditionsDialogue { get; set; }
        public TreeViewLeftPanel TreeViewLeftPanel { get; set; }
        public SeadragonCenterPanel SeadragonCenterPanel { get; set; }
        public PagingHeaderPanel PagingHeaderPanel { get; set; }
        public SearchFooterPanel SearchFooterPanel { get; set; }
    }

    public class ConditionsDialogue
    {
        public Dictionary<string, string> Content { get; set; }
    }

    public class HelpDialogue
    {
        public SimpleDialogue Content { get; set; }
    }

    public class SimpleDialogue
    {
        public string Title { get; set; }
        public string Text { get; set; }
    }
    
    public class TreeViewLeftPanel
    {
        public TreeViewLeftPanelOptions Options { get; set; }
    }
    public class TreeViewLeftPanelOptions
    {
        public bool PanelOpen { get; set; }
    }

    public class SeadragonCenterPanel
    {
        public SeadragonCenterPanelOptions Options { get; set; }
    }
    public class SeadragonCenterPanelOptions
    {
        public bool TitleEnabled { get; set; }
    }
    
    public class PagingHeaderPanel
    {
        public PagingHeaderPanelOptions Options { get; set; }
    }
    public class PagingHeaderPanelOptions
    {
        public bool HelpEnabled { get; set; }
        public bool ModeOptionsEnabled { get; set; }
    }

    public class SearchFooterPanel
    {
        public SearchFooterPanelOptions Options { get; set; }
    }
    public class SearchFooterPanelOptions
    {
        public bool MinimiseButtons { get; set; }
    }
}