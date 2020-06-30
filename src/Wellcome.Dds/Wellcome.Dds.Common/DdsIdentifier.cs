namespace Wellcome.Dds.Common
{
    public class DdsIdentifier
    {
        public const char Underscore = '_';
        public const char Slash = '/';
        public static readonly char[] Separators = { Underscore, Slash };
        private readonly string[] _parts;

        public string this[int index]
        {
            get { return _parts[index]; }
        }

        public string BNumber
        {
            get { return _parts[0]; }
        }
        public string VolumePart
        {
            get
            {
                if (_parts.Length > 1)
                {
                    return _parts[0] + Underscore + _parts[1];
                }
                return null;
            }
        }
        public string IssuePart
        {
            get
            {

                if (_parts.Length > 2)
                {
                    return _parts[0] + Underscore + _parts[1] + Underscore + _parts[2];
                }
                return null;
            }
        }

        public int SequenceIndex { get; private set; }

        public IdentifierType IdentifierType { get; private set; }

        readonly string _value;
        public DdsIdentifier(string value)
        {
            IdentifierType = IdentifierType.Unknown;
            _value = value;
            _parts = value.Split(Separators);
            if (_parts.Length == 1)
            {
                IdentifierType = IdentifierType.BNumber;
            }
            if (_parts.Length == 2 && _value.StartsWith(_parts[0] + Underscore))
            {
                IdentifierType = IdentifierType.Volume;
            }
            if (_parts.Length == 2 && _value.StartsWith(_parts[0] + Slash))
            {
                SequenceIndex = int.Parse(_parts[1]);
                IdentifierType = IdentifierType.BNumberAndSequenceIndex;
            }
            if (_parts.Length == 3)
            {
                IdentifierType = IdentifierType.Issue;
            }
        }

        public static implicit operator string(DdsIdentifier di)
        {
            return di.ToString();
        }
        public static implicit operator DdsIdentifier(string di)
        {
            return new DdsIdentifier(di);
        }

        public override string ToString()
        {
            return _value;
        }
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
