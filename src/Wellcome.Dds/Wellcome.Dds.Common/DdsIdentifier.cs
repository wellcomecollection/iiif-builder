namespace Wellcome.Dds.Common
{
    public class DdsIdentifier
    {
        public const char Underscore = '_';
        public const char Slash = '/';
        public static readonly char[] Separators = { Underscore, Slash };
        private readonly string[] parts;
        private readonly string value;

        public string this[int index] => parts[index];

        public string BNumber => parts[0];

        public string VolumePart
        {
            get
            {
                if (parts.Length > 1)
                {
                    return parts[0] + Underscore + parts[1];
                }
                return null;
            }
        }
        public string IssuePart
        {
            get
            {

                if (parts.Length > 2)
                {
                    return parts[0] + Underscore + parts[1] + Underscore + parts[2];
                }
                return null;
            }
        }
        
        public int SequenceIndex { get; }

        public IdentifierType IdentifierType { get; }

        public DdsIdentifier(string value)
        {
            IdentifierType = IdentifierType.Unknown;
            this.value = value;
            parts = value.Split(Separators);
            if (parts.Length == 1)
            {
                IdentifierType = IdentifierType.BNumber;
            }
            if (parts.Length == 2 && this.value.StartsWith(parts[0] + Underscore))
            {
                IdentifierType = IdentifierType.Volume;
            }
            if (parts.Length == 2 && this.value.StartsWith(parts[0] + Slash))
            {
                SequenceIndex = int.Parse(parts[1]);
                IdentifierType = IdentifierType.BNumberAndSequenceIndex;
            }
            if (parts.Length == 3)
            {
                IdentifierType = IdentifierType.Issue;
            }
        }

        public static implicit operator string(DdsIdentifier di) => di.ToString();

        public static implicit operator DdsIdentifier(string di) => new DdsIdentifier(di);

        public override string ToString() => value;
    }

    public enum IdentifierType
    {
        BNumber,
        Volume,
        Issue,
        BNumberAndSequenceIndex,
        Unknown
    }
}
