using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace Wellcome.Dds.WordsAndPictures
{
    [Serializable]
    [ProtoContract]
    public class Word
    {
        #nullable disable
        
        public Word()
        {
        }
        
        public Word(string contentRaw, string contentNorm)
        {
            ContentRaw = contentRaw;
            ContentNorm = contentNorm;
        }

        [ProtoMember(1)] public string ContentRaw;

        [ProtoMember(2)] public string ContentNorm;

        [ProtoMember(3)] public int X;

        [ProtoMember(4)] public int Y;

        [ProtoMember(5)] public int W;

        [ProtoMember(6)] public int H;

        /// <summary>
        /// The unique word number within the document; use this to tell if two words are adjacent in the text
        /// </summary>
        [ProtoMember(7)] public int Wd;

        /// <summary>
        /// The unique line number within the document; use this to tell if two words are on the same line
        /// </summary>
        [ProtoMember(8)] public int Li;

        /// <summary>
        /// The size of the space between this word and the next (required for coalescing)
        /// </summary>
        [ProtoMember(9)] public int Sp;

        /// <summary>
        /// The image number within the document that this word is on
        /// </summary>
        [ProtoMember(10)] public int Idx;

        /// <summary>
        /// The position of this word within the full normalised text
        /// </summary>
        [ProtoMember(11)] public int PosNorm;

        /// <summary>
        /// The position of this word within the raw text
        /// </summary>
        [ProtoMember(12)]
        public int PosRaw;

        public int LenNorm => ContentNorm.Length;

        public int LenRaw => ContentRaw.Length;

        public override string ToString() => ContentNorm;

        public string ToRawString() => ContentRaw;
    }
}
