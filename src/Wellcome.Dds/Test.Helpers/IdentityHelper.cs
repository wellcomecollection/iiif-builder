using Wellcome.Dds.Common;

namespace Test.Helpers;

public class IdentityHelper
{
    public static DdsIdentity GetSimpleTestBNumber(string bNumber)
    {
        return new DdsIdentity
        {
            PackageIdentifier = bNumber,
            Source = Source.Sierra,
            PackageIdentifierPathElementSafe = bNumber,
            PathElementSafe = bNumber,
            Value = bNumber,
            Generator = Generator.Goobi,
            IsPackageLevelIdentifier = true,
            StorageSpace = StorageSpace.Digitised
        };
    }
}