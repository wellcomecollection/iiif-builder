using Wellcome.Dds.Common;

namespace Test.Helpers;

public class IdentityHelper
{
    public static DdsIdentity GetSimpleTestBNumber(string bNumber)
    {
        return new DdsIdentity
        {
            Value = bNumber,
            LowerCaseValue = bNumber.ToLowerInvariant(),
            PackageIdentifier = bNumber,
            Source = Source.Sierra,
            PackageIdentifierPathElementSafe = bNumber,
            PathElementSafe = bNumber,
            Generator = Generator.Goobi,
            IsPackageLevelIdentifier = true,
            StorageSpace = StorageSpace.Digitised,
            Level = IdentifierLevel.Package
        };
    }
}