using System;
using System.Collections.Generic;
using System.Linq;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Dlcs.Model;
using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.AssetDomainRepositories.DigitalObjects
{
    /// <summary>
    /// The roles and size fields the DDS gives a DLCS image, and how it compares them with what the DLCS holds,
    /// for each <see cref="DlcsSizeModel"/>. See docs/dlcs-size-model.md.
    /// </summary>
    public class DlcsImageAccess
    {
        /// <summary>
        /// The role Protagonist 1.13.1+ gives an image with no roles and maxUnauthorised of 0 or more, so that
        /// only /full/ requests up to openFullMax are served, to anyone.
        /// </summary>
        public const string UnobtainableRole = "https://dlcs.io/roles/unobtainable";

        /// <summary>
        /// Legacy: maxUnauthorised for an open image whose METS doesn't permit wholeImageHighResAsJpg
        /// </summary>
        public const int LegacyRestrictedMaxUnauthorised = 1000;

        /// <summary>
        /// The size a clickthrough image is available at before the user has clicked through
        /// </summary>
        public const int ClickthroughOpenSize = 200;

        private readonly DlcsSizeModel sizeModel;
        private readonly int restrictedDownloadMaxWidth;
        private readonly bool holdAccessChanges;

        public DlcsImageAccess(DlcsSizeModel sizeModel, int restrictedDownloadMaxWidth, bool holdAccessChanges)
        {
            this.sizeModel = sizeModel;
            // Protagonist stores a negative maxWidth as 0, so comparing against a negative setting would
            // find a mismatch, and re-ingest, on every sync
            this.restrictedDownloadMaxWidth = Math.Max(0, restrictedDownloadMaxWidth);
            this.holdAccessChanges = holdAccessChanges;
        }

        public DlcsSizeModel SizeModel => sizeModel;

        /// <summary>
        /// Whether open images in the manifestation can be had at full size
        /// </summary>
        public static bool PermitsHighRes(IManifestation manifestation)
            => manifestation.PermittedOperations.Contains("wholeImageHighResAsJpg");

        /// <summary>
        /// The largest response the DLCS gives anyone for an open image, on either side; 0 for no limit.
        /// The DLCS doesn't make larger thumbnails for it either, so the IIIF the DDS builds lists no larger sizes.
        /// </summary>
        /// <param name="permitsHighRes">The manifestation's METS permits wholeImageHighResAsJpg</param>
        public int OpenImageMaxSize(bool permitsHighRes)
            => permitsHighRes ? 0
                : sizeModel == DlcsSizeModel.Legacy ? LegacyRestrictedMaxUnauthorised
                : restrictedDownloadMaxWidth;

        /// <summary>
        /// Set the roles and size fields on an image the DDS expects the DLCS to have.
        /// </summary>
        /// <param name="image">The image to set them on</param>
        /// <param name="isImage">True for the image family; AV and files are never size-limited</param>
        /// <param name="roles">The roles from the file's access condition; empty if it's open</param>
        /// <param name="permitsHighRes">The manifestation's METS permits wholeImageHighResAsJpg</param>
        /// <param name="isClickthrough">The role is the clickthrough (requires registration) role</param>
        public void Apply(Image image, bool isImage, string[] roles, bool permitsHighRes, bool isClickthrough)
        {
            image.Roles = roles;
            if (sizeModel == DlcsSizeModel.Legacy)
            {
                image.MaxUnauthorised = !isImage ? -1
                    : roles.Length > 0 ? (isClickthrough ? ClickthroughOpenSize : 0)
                    : permitsHighRes ? -1 : LegacyRestrictedMaxUnauthorised;
                image.MaxWidth = null;
                image.OpenFullMax = null;
                return;
            }

            // The values Protagonist's migration gives Legacy registrations, except for an open image that
            // doesn't permit high-res: no role and a maxWidth, rather than the unobtainable role and openFullMax
            image.MaxUnauthorised = null;
            image.MaxWidth = isImage && roles.Length == 0 && !permitsHighRes ? restrictedDownloadMaxWidth : 0;
            image.OpenFullMax = isImage && isClickthrough ? ClickthroughOpenSize : 0;
        }

        /// <summary>
        /// The size fields that differ, each of which needs a re-ingest: the DLCS doesn't allow them in a batch
        /// PATCH, and they change where its thumbnails are stored.
        /// </summary>
        public List<(string Field, int? New, int? Existing)> SizeMismatches(Image newImage, Image existingImage)
        {
            var mismatches = new List<(string, int?, int?)>();
            if (sizeModel == DlcsSizeModel.Legacy)
            {
                if (existingImage.MaxUnauthorised != newImage.MaxUnauthorised)
                {
                    mismatches.Add(("maxUnauthorised", newImage.MaxUnauthorised, existingImage.MaxUnauthorised));
                }
                return mismatches;
            }

            // 1.13.1+ stores 0 for "not set" and never returns null for these
            if ((existingImage.MaxWidth ?? 0) != (newImage.MaxWidth ?? 0))
            {
                mismatches.Add(("maxWidth", newImage.MaxWidth, existingImage.MaxWidth));
            }
            if ((existingImage.OpenFullMax ?? 0) != (newImage.OpenFullMax ?? 0))
            {
                mismatches.Add(("openFullMax", newImage.OpenFullMax, existingImage.OpenFullMax));
            }
            return mismatches;
        }

        /// <summary>
        /// Whether the DLCS's roles are the ones the DDS expects.
        /// Under Legacy, an image the DDS registers with no roles and maxUnauthorised of 0 or more has the
        /// unobtainable role on Protagonist 1.13.1+ (from the API's conversion, or the migration). That's the same
        /// behaviour, so it matches. Removing the role would make Protagonist 1.14+ re-ingest the image, and it
        /// would come back with no size limit at all.
        /// </summary>
        public bool RolesMatch(Image newImage, Image existingImage)
        {
            var newRoles = newImage.Roles ?? Array.Empty<string>();
            var existingRoles = existingImage.Roles ?? Array.Empty<string>();
            if (newRoles.SequenceEqual(existingRoles))
            {
                return true;
            }

            return sizeModel == DlcsSizeModel.Legacy
                   && newRoles.Length == 0
                   && newImage.MaxUnauthorised >= 0
                   && existingRoles.Length == 1 && existingRoles[0] == UnobtainableRole;
        }

        /// <summary>
        /// With HoldAccessChanges on, give the new image the existing image's roles and size fields, so the sync
        /// sends no access change, and say what was held. Does nothing (and returns nothing) when it's off.
        /// </summary>
        public List<string> Hold(Image newImage, Image existingImage)
        {
            var held = new List<string>();
            if (!holdAccessChanges)
            {
                return held;
            }

            if (!RolesMatch(newImage, existingImage))
            {
                held.Add(HeldMessage("roles",
                    string.Join(",", newImage.Roles ?? Array.Empty<string>()),
                    string.Join(",", existingImage.Roles ?? Array.Empty<string>())));
            }
            foreach (var (field, newValue, existingValue) in SizeMismatches(newImage, existingImage))
            {
                held.Add(HeldMessage(field, newValue?.ToString(), existingValue?.ToString()));
            }

            if (held.Count > 0)
            {
                newImage.Roles = existingImage.Roles ?? Array.Empty<string>();
                if (sizeModel == DlcsSizeModel.Legacy)
                {
                    newImage.MaxUnauthorised = existingImage.MaxUnauthorised;
                }
                else
                {
                    newImage.MaxWidth = existingImage.MaxWidth ?? 0;
                    newImage.OpenFullMax = existingImage.OpenFullMax ?? 0;
                }
            }
            return held;
        }

        /// <summary>
        /// Explicit needs Protagonist 1.13.1 or later. Earlier versions don't return openFullMax, so any image
        /// without it shows the DLCS is too old. (A manifestation with no images on the DLCS can't tell.)
        /// </summary>
        public bool DlcsLacksSizeFields(IEnumerable<Image> imagesOnDlcs)
            => sizeModel == DlcsSizeModel.Explicit && imagesOnDlcs.Any(image => image.OpenFullMax == null);

        private static string HeldMessage(string field, string? newValue, string? existingValue)
            => $"HELD (Dlcs:HoldAccessChanges) mismatch for {field} - new: '{newValue}', existing: '{existingValue}'";
    }
}
