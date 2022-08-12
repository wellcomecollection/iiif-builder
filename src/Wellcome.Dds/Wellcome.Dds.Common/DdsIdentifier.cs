namespace Wellcome.Dds.Common
{
    public class DdsIdentifier
    {
        private const char Underscore = '_';
        private const char Slash = '/';
        private const string BornDigital = "born-digital";
        private const string Digitised = "digitised";
        private static readonly char[] Separators = { Underscore, Slash };
        private readonly string[] parts;
        private readonly string value;

        /// <summary>
        /// The identifier for the stored digital object of which this ID might be whole or part.
        /// This is needed to obtain the object from the storage service, or look up a record in the Catalogue API.
        /// 
        /// The majority of DdsIdentifiers are the same string as their packageIdentifiers - e.g.,
        /// b12312312 or PPCRI/A/B/C
        /// But for multiple manifestations, different identifiers belong to the same package:
        /// b19974760_10 and b19974760_10_10 have the same PackageIdentifier, b19974760.
        /// </summary>
        public string PackageIdentifier { get; }
        
        /// <summary>
        /// A version of the PackageIdentifier that would only be one path element in a URL or file name
        /// </summary>
        public string PackageIdentifierPathElementSafe { get; }
        
        /// <summary>
        /// What prefix in the storage service API is required to locate this object's files
        /// Currently either "digitised" or "born-digital"
        /// </summary>
        public string StorageType { get; }
        
        /// <summary>
        /// Whether this identifier starts with a BNumber
        /// </summary>
        public bool HasBNumber { get; }

        /// <summary>
        /// If the identifier starts with a b number, the value of that b number.
        /// </summary>
        public string BNumber { get; }

        /// <summary>
        /// If the identifier is or is part of a volume within a multiple manifestation, the value of the volume identifier.
        ///
        /// e.g., if the identifier is b19974760_10_10, the volume identifier is b19974760_10
        /// </summary>
        public string VolumePart
        {
            get
            {
                if (HasBNumber && parts.Length > 1)
                {
                    return parts[0] + Underscore + parts[1];
                }
                return null;
            }
        }
        
        /// <summary>
        /// If the identifier is an issue within a volume within a multiple manifestation, the value of the issue identifier.
        ///
        /// e.g., if the identifier is b19974760_10_10, the issue identifier is also b19974760_10_10
        /// If the identifier is b19974760_10 the issue identifier is null.
        /// </summary>
        public string IssuePart
        {
            get
            {
                if (HasBNumber && parts.Length > 2)
                {
                    return parts[0] + Underscore + parts[1] + Underscore + parts[2];
                }
                return null;
            }
        }
        
        /// <summary>
        /// This needs to be retired as part of this work.
        /// </summary>
        public int SequenceIndex { get; }

        /// <summary>
        /// What kind of digital object does this Identifier represent?
        /// In the DDS we infer type from the form of the identifier, it's not an opaque string.
        /// </summary>
        public IdentifierType IdentifierType { get; }


        public DdsIdentifier(string value)
        {
            IdentifierType = IdentifierType.NonBNumber;
            this.value = value;
            parts = value.Split(Separators);
            if (parts.Length > 0 && parts[0].IsBNumber())
            {
                HasBNumber = true;
                BNumber = parts[0];
                PackageIdentifier = BNumber;
                PackageIdentifierPathElementSafe = BNumber;
                StorageType = Digitised;
            }
            if (parts.Length == 1 && HasBNumber)
            {
                IdentifierType = IdentifierType.BNumber;
            }
            if (parts.Length == 2 && HasBNumber && this.value.StartsWith(parts[0] + Underscore))
            {
                IdentifierType = IdentifierType.Volume;
            }
            if (parts.Length == 2 && HasBNumber && this.value.StartsWith(parts[0] + Slash))
            {
                SequenceIndex = int.Parse(parts[1]);
                IdentifierType = IdentifierType.BNumberAndSequenceIndex;
            }
            if (parts.Length == 3 && HasBNumber)
            {
                IdentifierType = IdentifierType.Issue;
            }

            if (IdentifierType == IdentifierType.NonBNumber)
            {
                // The supplied value was not a b number, or something that started with a b number.
                // Is there anything that would let us distinguish between a CALM ID and some other string?
                // For now we can assume that the identifier might be born-digital, but we can no longer
                // dismiss is as invalid at this stage. We are assuming that anything that doesn't start with a 
                // BNumber is a potential born digital identifier.
                // TODO: Can we validate this just from the string?

                StorageType = BornDigital;
                PackageIdentifier = this.value.ToCalmForm();
                PackageIdentifierPathElementSafe = PackageIdentifier.Replace(Slash, Underscore);
            }
        }

        public static implicit operator string(DdsIdentifier di) => di.ToString();

        public static implicit operator DdsIdentifier(string di) => new(di);

        public override string ToString() => value;
    }

    public enum IdentifierType
    {
        BNumber,
        Volume,
        Issue,
        BNumberAndSequenceIndex,
        NonBNumber
    }
}
