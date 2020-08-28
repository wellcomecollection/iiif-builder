using System;
using System.Collections.Generic;
using System.Text;

namespace Wellcome.Dds.WordsAndPictures
{

    [Serializable]
    public class Word
    {
        public string ContentRaw;
        public string ContentNorm;

        public Word(string contentRaw, string contentNorm)
        {
            ContentRaw = contentRaw;
            ContentNorm = contentNorm;
        }

        public int X;
        public int Y;
        public int W;
        public int H;

        /// <summary>
        /// The unique word number within the document; use this to tell if two words are adjacent in the text
        /// </summary>
        public int Wd;

        /// <summary>
        /// The unique line number within the document; use this to tell if two words are on the same line
        /// </summary>
        public int Li;

        /// <summary>
        /// The size of the space between this word and the next (required for coalescing)
        /// </summary>
        public int Sp;

        /// <summary>
        /// The image number within the document that this word is on
        /// </summary>
        public int Idx;

        /// <summary>
        /// The position of this word within the full normalised text
        /// </summary>
        public int PosNorm;

        /// <summary>
        /// The position of this word within the raw text
        /// </summary>
        public int PosRaw;


        public int LenNorm
        {
            get { return ContentNorm.Length; }
        }

        public int LenRaw
        {
            get { return ContentRaw.Length; }
        }

        public override string ToString()
        {
            return ContentNorm;
        }

        public string ToRawString()
        {
            return ContentRaw;
        }
    }

}
