namespace Wellcome.Dds.Common;

public interface IIdentityService
{
    DdsIdentity GetIdentity(string s);
    DdsIdentity GetIdentity(string s, string? generator);

    /// <summary>
    /// Resolve a volume or issue identifier that the caller has confirmed really exists, because
    /// it has been enumerated from its package's METS. Implementations may persist the identity;
    /// plain GetIdentity reads never create volume/issue records, because any requested URL can
    /// name a volume that doesn't exist.
    /// </summary>
    DdsIdentity RegisterAuthoritativeChild(string s);
}