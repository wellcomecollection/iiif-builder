namespace Wellcome.Dds.AssetDomain.Dlcs
{
    /// <summary>
    /// How the DDS tells the DLCS how large an image may be, and to whom.
    /// </summary>
    public enum DlcsSizeModel
    {
        /// <summary>
        /// Send maxUnauthorised and roles, as for Protagonist up to 1.12. Protagonist 1.13.1 and later still accept
        /// maxUnauthorised and convert it: an image with no roles and maxUnauthorised of 0 or more gets the
        /// unobtainable role, and openFullMax takes the value of maxUnauthorised.
        /// </summary>
        Legacy,

        /// <summary>
        /// Send roles, maxWidth and openFullMax, and never maxUnauthorised. Needs Protagonist 1.13.1 or later.
        /// </summary>
        Explicit
    }
}
