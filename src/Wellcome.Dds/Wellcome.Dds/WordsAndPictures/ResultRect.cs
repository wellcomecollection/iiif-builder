using System.Collections.Generic;
using System.Linq;

namespace Wellcome.Dds.WordsAndPictures
{
    public class ResultRect
    {
        public string ContentNorm;
        public string ContentRaw;

        public ResultRect(string contentNorm, string contentRaw)
        {
            ContentRaw = contentRaw;
            ContentNorm = contentNorm;
        }

        public int X;
        public int Y;
        public int W;
        public int H;

        /// <summary>
        /// The unique word numbers within the document that make this result; use this to tell if two words are adjacent in the text
        /// </summary>
        public List<int>? Wds;

        /// <summary>
        /// The positions of each word in the rectangle withint the normalised text
        /// </summary>
        public List<int>? PosNorms;

        /// <summary>
        /// The unique line number within the document that this rect is on
        /// </summary>
        public int Li;

        /// <summary>
        /// The size of the space between this word and the next (required for coalescing).
        /// In some ALTO sources, the word might not have a following space
        /// </summary>
        public int Sp;

        /// <summary>
        /// The image number within the document that this result is on
        /// </summary>
        public int Idx;

        /// <summary>
        /// The hit number of this result. Two or more results can share the same hit number if the result is two or
        /// more rectangles (e.g., starts in one line and finishes on another).
        /// </summary>
        public int Hit;

        /// <summary>
        /// The position of the word in the normalised text
        /// </summary>
        public int PosNorm => PosNorms?.First() ?? 0;

        /// <summary>
        /// The position of the word in the raw text
        /// </summary>
        public int PosRaw;

        /// <summary>
        /// Context - words on either side of this word
        /// </summary>
        public string? Before;
        public string? After;

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

        public static ResultRect FromWord(Word word, int hit)
        {
            return new ResultRect(word.ToString(), word.ToRawString())
            {
                X = word.X,
                Y = word.Y,
                W = word.W,
                H = word.H,
                Li = word.Li,
                Sp = word.Sp,
                Idx = word.Idx,
                Wds = new List<int> { word.Wd },
                Hit = hit,
                PosNorms = new List<int> { word.PosNorm },
                PosRaw = word.PosRaw
            };
        }

        public ResultRect ShallowCopy()
        {
            return (ResultRect)MemberwiseClone();
        }
    }
}
