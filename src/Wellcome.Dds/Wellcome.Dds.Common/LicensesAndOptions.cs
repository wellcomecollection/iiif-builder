using System;
using System.Collections.Generic;
using System.Linq;

namespace Wellcome.Dds.Common
{
    /// <summary>
    /// This is the new mechanism for determining options in the player. The METS file will have a single integer value
    /// in mods:accessCondition type="player", which can be bit-compared to these flags to determine what options should be
    /// output in the package file.
    /// This replaces the old mechanism where the permitted operations are inferred from the license codes and the
    /// asset type (see the LicensesAndOptions.LicenseOptions structure, which holds this map).
    /// 
    /// There is a new 
    /// </summary>
    [Flags]
    public enum PlayerOptions
    {
        CurrentViewAsJpg = 1,
        WholeImageLowResAsJpg = 2,
        WholeImageHighResAsJpg = 4,
        EntireDocumentAsPdf = 8,
        EntireFileAsOriginal = 16,
        Embed = 32 // embed is a bit different because it is not authorised by DdsServiceModule per request
    }

    public sealed class LicensesAndOptions
    {
        // John Skeet's type 4 singleton pattern
        private static readonly LicensesAndOptions InternalInstance = new LicensesAndOptions();
        static LicensesAndOptions() { }
        public static LicensesAndOptions Instance => InternalInstance;

        public string[]? DownloadOptions { get; private set; }
        public string[]? OperationNames { get; private set; }
        public Dictionary<string, Dictionary<string, int[]>> LicenseOptions { get; private set; }

        /// <summary>
        /// converts the operation name in the URI (e.g., /crop/, /actual...) to a named permission
        /// (e.g., "crop" => "currentViewAsJpg", "actual" => "wholeImageHighResAsJpg")
        /// </summary>
        /// <param name="op"></param>
        /// <returns></returns>
        public string? GetDownloadOptionForControllerOperation(string op)
        {
            if (OperationNames == null || DownloadOptions == null)
            {
                return null;
            }
            int idx = Array.IndexOf(OperationNames, op);
            if (idx >= 0 && idx < DownloadOptions.Length)
            {
                return DownloadOptions[idx];
            }
            return null;
        }

        public PlayerOptions GetFlagsFromCode(int playerOptionsCode, string assetType)
        {
            var flags = (PlayerOptions)playerOptionsCode;
            // return flags;

            // Really, that should be it. But for historical reasons, "EntireFileAsOriginal" is going to be present
            // in the flags for most monographs even when it should NOT be. So the DDS is going to override this flag
            // for deep zoom
            if (assetType == "seadragon/dzi" || assetType == "image/jp2")
            {
                // this operation removes it whether it is set or not. Using the XOR (^) operator
                // would work ONLY IF WE KNEW THE FLAG WAS SET - which we could test for. But this is more elegant.
                flags &= ~PlayerOptions.EntireFileAsOriginal;
            }
            return flags;

        }

        public bool IsAllowedDownloadOption(string dzLicenseCode, string sectionType, string assetType, string downloadOption)
        {
            return GetPermittedOperations(dzLicenseCode, sectionType, assetType).Contains(downloadOption);
        }


        public bool IsAllowedOperation(int playerOptionsCode, string assetType, string controllerOption)
        {
            var flags = GetFlagsFromCode(playerOptionsCode, assetType);
            var option = GetDownloadOptionForControllerOperation(controllerOption);
            if (option == null)
            {
                return false;
            }
            var asFlag = (PlayerOptions)Enum.Parse(typeof(PlayerOptions), option, true);
            return flags.HasFlag(asFlag);
        }

        public bool IsAllowedOperation(string dzLicenseCode, string sectionType, string assetType, string op)
        {
            var option = GetDownloadOptionForControllerOperation(op);if (option == null)
            {
                return false;
            }
            return IsAllowedDownloadOption(dzLicenseCode, sectionType, assetType, option);
        }

        public string[] GetPermittedOperations(int playerOptions, string assetType)
        {
            var optionsType = typeof(PlayerOptions);
            var flags = GetFlagsFromCode(playerOptions, assetType);
            var permittedOps =
                from PlayerOptions option in Enum.GetValues(optionsType)
                where (flags & option) == option
                select Enum.GetName(optionsType, option);
            // need to lower case the first letter
            return permittedOps.Select(op => Char.ToLower(op[0]) + op.Substring(1)).ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dzLicenseCode">Single character license code</param>
        /// <param name="sectionType">e.g., "Monograph", "Artwork"</param>
        /// <param name="assetType">e.g., "seadragon/dzi", "application/pdf"</param>
        /// <returns></returns>
        public string[] GetPermittedOperations(string dzLicenseCode, string sectionType, string assetType)
        {
            var permittedOps = new List<string>();
            if (LicenseOptions.TryGetValue(
                    sectionType.ToLowerInvariant(), out Dictionary<string, int[]>? optsForType))
            {
                var licenseCode = dzLicenseCode.ToLowerInvariant();
                int[]? opts;
                if (optsForType.TryGetValue(licenseCode, out opts))
                {
                    /* ########################################### */
                    // temporary hack until we can get rid of the assumption that a "monograph" is an image sequence
                    if (
                        (licenseCode == "r" || licenseCode == "s") &&
                        sectionType.ToLowerInvariant() == "monograph" &&
                        assetType.ToLowerInvariant() == "application/pdf")
                    {
                        opts = new[] { 4 };
                    }
                    /* ########################################### */

                    for (int idx = 0; idx < opts.Length; idx++)
                    {
                        if (DownloadOptions != null && idx < DownloadOptions.Length)
                        {
                            permittedOps.Add(DownloadOptions[opts[idx]]);
                        }
                    }
                }
            }
            return permittedOps.ToArray();
        }


        private LicensesAndOptions()
        {
            // this first table contains the codes for the various types of downloads and their display text.
            DownloadOptions = new[] {                                  // entry in options below
                                          "currentViewAsJpg",          // 0
                                          "wholeImageHighResAsJpg",    // 1
                                          "wholeImageLowResAsJpg",     // 2
                                          "entireDocumentAsPdf",       // 3
                                          "entireFileAsOriginal"       // 4
                                    };

            // This table holds the url patterns of the controllers that correspond to these operations
            OperationNames = new[] {                // entry in options below
                                     "crop",        // 0 
                                     "actual",      // 1
                                     "confine",     // 2
                                     "pdf",         // 3
                                     "media"        // 4
                                   };

            // This structure tells us which operations are permitted for a given type and code
            LicenseOptions = new Dictionary<string, Dictionary<string, int[]>>
            {
                {
                    "monograph", new Dictionary<string, int[]>
                    {
                        {"a", new[] {0, 1, 2, 3}},
                        {"b", new[] {0, 1, 2, 3}},
                        {"c", new[] {0, 1, 2, 3}},
                        {"d", new[] {0, 1, 2, 3}},
                        {"e", new[] {0, 1, 2}},
                        {"f", new[] {0, 1, 2}},
                        {"g", new[] {0, 1, 2}},
                        {"k", new[] {0, 1, 2, 3}},
                        {"r", new[] {0, 1, 2, 3}},
                        {"s", new[] {0, 1, 2, 3}}
                    }
                },
                {
                    "archive", new Dictionary<string, int[]>
                    {
                        {"j", new[] {0, 1, 2, 3}},
                        {"a", new[] {0, 1, 2, 3}} // TODO: "a" added temporarily for b19662506
                    }
                },
                {
                    "boundManuscript", new Dictionary<string, int[]>
                    {
                        {"a", new[] {0, 1, 2}},
                        {"j", new[] {0, 1, 2}}
                    }
                },
                {
                    "video", new Dictionary<string, int[]>
                    {
                        {"a", new[] {4}},
                        {"b", new[] {4}},
                        {"c", new[] {4}},
                        {"d", new[] {4}},
                        {"k", new[] {4}}
                    }
                },
                {
                    "audio", new Dictionary<string, int[]>
                    {
                        {"a", new[] {4}},
                        {"b", new[] {4}},
                        {"c", new[] {4}},
                        {"d", new[] {4}},
                        {"k", new[] {4}}
                    }
                },
                {
                    "artwork", new Dictionary<string, int[]>
                    {
                        {"a", new[] {0, 1, 2, 4}},
                        {"b", new[] {0, 1, 2}},
                        {"c", new[] {0, 1, 2}},
                        {"d", new[] {0, 1, 2}},
                        {"j", new[] {0, 1, 2}},
                        {"k", new[] {0, 1, 2}},
                        {"l", new[] {0, 2}},
                        {"m", new[] {0, 2}},
                        {"n", new[] {0, 2}},
                        {"o", new[] {0, 2}},
                        {"p", new[] {0, 2}},
                        {"q", new[] {0, 2}}
                    }
                }
            };
        }
    }
}
