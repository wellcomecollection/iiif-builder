using System;

namespace Wellcome.Dds.AssetDomain.DigitalObjects
{
    public interface IPdf
    {
        // The DLCS URL of the PDF
        string? Url { get; set; }
        // False if the DLCS hasn't started or finished making it yet
        bool Exists { get; set; }
        //true if the DLCS has started the creation process
        bool InProcess { get; set; }
        // When the creation process finished, if it exists
        DateTime? Created { get; set; }
        // The deduced roles, from the constituent images, for the DLCS to enforce access control.
        // The hard-coded rule is that open and clickthrough are included, anything else is replaced by a placeholder page.
        // So this in practice for Wellcome will either be empty, or clickthrough.
        string[]? Roles { get; set; }

        int PageCount { get; set; }
        long SizeBytes { get; set; }
    }
}
