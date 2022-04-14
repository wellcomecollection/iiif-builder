using System;

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

        // At the moment this kind of means the same thing as package identifier.
        // the DDSIdentifier might represent a particular Manifestation 
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

        // I don't like this very much, it's the one place where meaning is derived from
        // the string form of an identifier. It's only used in one place and if we can
        // avoid that one place then it can be removed.
        public DdsIdentifier Parent
        {
            get
            {
                switch (IdentifierType)
                {
                    case IdentifierType.BNumber:
                        return null;
                    case IdentifierType.Volume:
                        return BNumber;
                    case IdentifierType.BNumberAndSequenceIndex:
                        // TODO: This is invalid for C&D
                        return BNumber;
                    case IdentifierType.Unknown:
                        return null;
                    case IdentifierType.Issue:
                        // TODO: C&D only; come back to this
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                throw new NotImplementedException();
            }
        }

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
