using System;
using System.Collections.Generic;
using System.Linq;
using Utils;
using Wellcome.Dds.WordsAndPictures.TextArtefacts;

namespace Wellcome.Dds.WordsAndPictures
{
    /// <summary>
    /// This holds the full text for a SEQUENCE of ALTO files, i.e., the full text for a whole book.
    /// An ALTO file is an XML document for a single digitised image, but we need the whole text of a book
    /// so we can search in it.
    /// 
    /// It does not follow the object model of ALTO (textblocks, textline etc) because that would not be 
    /// very efficient. We want to keep this as small as possible so we can serialise it, cache it in memory
    /// etc. It is already pretty large, and work needs to be done on reducing the serialised size.
    /// 
    /// From a performance point of view it is optimised for searching strings, in the NormalisedFullText property.
    /// This is just a great big string containing the full text of the work with all punctuation etc removed.
    /// The position of each word in this string is also a key into the Words dictionary, so we can search the full string for
    /// "cat" using indexOf, then recover the Word object for "cat" from the Words dictionary. Then we can obtain
    /// its position and size.
    /// 
    /// the Text class also allows us to recover the text for a single image, because we store information that tells us
    /// how the text is split across the images.
    /// </summary>
    [Serializable]
    public class Text
    {
        public string NormalisedFullText { get; set; }
        public string RawFullText { get; set; }

        // the int is the indexOf the Word in the NormalisedFullText
        public Dictionary<int, Word> Words { get; set; }

        // TODO - not sure this is serializing properly
        public Dictionary<string, HashSet<string>> AutoCompleteBuckets { get; set; }

        // keeps track of the individual source images in the text
        public Image[] Images { get; set; }

        // identifies composed block elements in the text, used for tables
        public ComposedBlock[] ComposedBlocks { get; set; }

        // for both the above we will want to reconstruct a page from the flow of words. From an image we can get the start character,
        // and the start character of the following image, and take a substring from the normalisedText.

        // for composedblocks we can identify, for a given page, which of its words are part of the composed block.

        public static string Normalise(string s)
        {
            if (s.IsNullOrWhiteSpace())
            {
                return string.Empty;
            }
            return StringUtils.NormaliseSpaces(s.ToLowerInvariant().ToAlphanumericOrWhitespace());
            // TODO - should we remove (i.e., normalise) diacritics here too?
        }

        public ResultRect GetWord(int charPos, int wordPos)
        {
            Word word = null;
            ResultRect rr = null;
            if (charPos == -1 && wordPos != -1)
            {
                charPos = Words.Keys.OrderBy(x => x).ElementAt(wordPos);
            }
            if (charPos != -1)
            {
                while (!Words.ContainsKey(charPos) && charPos >= 0)
                {
                    charPos--;
                }
                word = Words[charPos];
            }
            if (word != null)
            {
                rr = ResultRect.FromWord(word, 0);
            }
            return rr;
        }

        public List<ResultRect> Search(string s)
        {
            s = Normalise(s);
            var results = new List<Word>();
            int startPos = 0;
            int matchPos;
            var hitMap = new Dictionary<int, int>(); // map of word number to hit number
            int hitCounter = 0;
            while ((matchPos = NormalisedFullText.IndexOf(s, startPos, StringComparison.InvariantCultureIgnoreCase)) != -1)
            {
                // this match might not be on a word boundary, so walk back to find the preceding space
                int padding = 0;
                while (matchPos > 0 && NormalisedFullText[matchPos - 1] != ' ')
                {
                    matchPos--;
                    padding++;
                }
                int matchLength = 0;
                var wordPos = matchPos;
                while (matchLength < (s.Length + padding))
                {
                    var word = Words[wordPos];
                    results.Add(word);
                    var lengthInText = word.LenNorm + 1;
                    matchLength += lengthInText;
                    wordPos += lengthInText;
                    hitMap[word.Wd] = hitCounter;
                }
                hitCounter++;
                startPos = matchPos + matchLength + 1;
                if (startPos >= (NormalisedFullText.Length - 1))
                {
                    break;
                }
            }
            return GetRectangles(s, results, hitMap);
        }

        // if you search for "my cat" the raw results will give you two words: "my" and "cat". If
        // these are adjacent and on the same line, then we should coalesce them into a single 
        // larger rectangle.
        private List<ResultRect> GetRectangles(string s, IList<Word> results, Dictionary<int, int> hitMap)
        {
            if (results.Count == 0)
            {
                return new List<ResultRect>(); // empty array in JSON
            }
            if (!s.Contains(" "))
            {
                // the search term is a single word, no need to coalesce results
                var rects = results.Select(ResultRect.FromWord).ToList();
                AddContext(rects);
                return rects;
            }
            var coalescedResults = new List<ResultRect>();
            var coalescedWord = ResultRect.FromWord(results[0], hitMap[results[0].Wd]);
            for (int i = 1; i < results.Count; i++)
            {
                var nextWord = results[i];
                if (coalescedWord.Li == nextWord.Li && coalescedWord.Wds.Last() == nextWord.Wd - 1)
                {
                    // these two words are adjacent on the same line
                    coalescedWord.Y = Math.Min(coalescedWord.Y, nextWord.Y);
                    coalescedWord.H = Math.Max(coalescedWord.H, nextWord.H);

                    // Earlier version - this only works if the ALTO file includes spacing information
                    //coalescedWord.W = coalescedWord.W + coalescedWord.Sp + nextWord.W;

                    // new version - calculates width based on nextWord's coordinates
                    coalescedWord.W = (nextWord.X + nextWord.W) - coalescedWord.X;
                    coalescedWord.Wds.Add(nextWord.Wd);
                    coalescedWord.PosNorms.Add(nextWord.PosNorm);
                    coalescedWord.Sp = nextWord.Sp;
                    coalescedWord.ContentNorm += " " + nextWord;
                    coalescedWord.ContentRaw += " " + nextWord.ToRawString();
                }
                else
                {
                    // add the previous coalesced word to the results
                    coalescedResults.Add(coalescedWord.ShallowCopy());
                    coalescedWord = ResultRect.FromWord(nextWord, hitMap[nextWord.Wd]);
                }
            }
            // add the last coalesced word
            coalescedResults.Add(coalescedWord);
            AddContext(coalescedResults);
            return coalescedResults;
        }

        private void AddContext(List<ResultRect> coalescedResults)
        {
            const int maxResultsWithContext = 200;
            const int snippetSize = 150;
            if (coalescedResults.Count < maxResultsWithContext)
            {
                foreach (var rect in coalescedResults)
                {
                    var hic = GetHitInContext(rect.PosNorms, snippetSize, snippetSize);
                    rect.Before = hic.Before;
                    rect.After = hic.After;
                    // compare rect.contentRaw with hic.contentRaw
                    //var preOffset = rect.PosNorm - snippetSize;
                    //if (preOffset < 0) preOffset = 0;
                    //var preOffsetLen = rect.PosNorm - preOffset;
                    //rect.Before = NormalisedFullText.Substring(preOffset, preOffsetLen);

                    //var postOffset = rect.PosNorm + rect.LenNorm;
                    //var postOffsetLen = snippetSize;
                    //if (postOffset + postOffsetLen > NormalisedFullText.Length)
                    //{
                    //    postOffsetLen = NormalisedFullText.Length - postOffset;
                    //}
                    //rect.After = NormalisedFullText.Substring(postOffset, postOffsetLen);
                }
            }
        }

        public HitInContext GetHitInContext(List<int> positionsInNormalisedText, int charactersBefore, int charactersAfter)
        {
            var hic = new HitInContext();
            var words = positionsInNormalisedText.Select(p => Words[p]).ToList();
            var posRaw = words.First().PosRaw;
            var preOffset = posRaw - charactersBefore;
            if (preOffset < 0) preOffset = 0;
            var preOffsetLen = posRaw - preOffset;
            hic.Before = RawFullText.Substring(preOffset, preOffsetLen);

            var posRawLast = words.Last().PosRaw;
            var postOffset = posRawLast + words.Last().LenRaw;
            var postOffsetLen = charactersAfter;
            if (postOffset + postOffsetLen > RawFullText.Length)
            {
                postOffsetLen = RawFullText.Length - postOffset;
            }
            hic.After = RawFullText.Substring(postOffset, postOffsetLen);

            hic.Hit = String.Join(" ", words.Select(w => w.ToRawString()));
            return hic;
        }

        public string[] GetSuggestions(string term)
        {
            term = Normalise(term);
            if (term.HasText() && term.Length > 2)
            {
                var key = term.Substring(0, 3);
                if (AutoCompleteBuckets.ContainsKey(key))
                {
                    var suggestions = AutoCompleteBuckets[key].Where(s => s.StartsWith(term));
                    return suggestions.OrderBy(s => s.Length).ThenBy(s => s).ToArray();
                }
            }
            return new string[] { };
        }

        // TODO: this code has not been tested on nested composedBlocks
        public Page GetPage(int imageIndex)
        {
            int start = Images[imageIndex].StartCharacter;
            int end = imageIndex < Images.Length - 1 ? Images[imageIndex + 1].StartCharacter : Int32.MaxValue;
            var wordKeys = Words.Keys.Where(k => k >= start && k < end).OrderBy(k => k);

            var page = new Page();
            var composedBlocks = ComposedBlocks.Where(cb => cb.ImageIndex == imageIndex).ToList();
            var blockStack = new Stack<Block>();
            blockStack.Push(page);
            foreach (var wordKey in wordKeys)
            {
                var blockThatStartsOnThisWord = composedBlocks.FirstOrDefault(cb => cb.StartCharacter == wordKey);
                if (blockThatStartsOnThisWord != null)
                {
                    if (blockStack.Peek().Blocks == null)
                    {
                        blockStack.Peek().Blocks = new List<Block>();
                    }
                    var b = new Block
                    {
                        ComposedBlock = blockThatStartsOnThisWord,
                        StartWordPosition = blockThatStartsOnThisWord.StartCharacter,
                        EndWordPosition = blockThatStartsOnThisWord.EndCharacter
                    };
                    blockStack.Peek().Blocks.Add(b);
                    blockStack.Push(b);
                }

                // we now are in the right block, which may be the page
                var block = blockStack.Peek();
                var word = Words[wordKey];
                if (block.Lines == null) block.Lines = new List<Line>();
                var line = block.Lines.SingleOrDefault(l => l.LineNumber == word.Li);
                if (line == null)
                {
                    line = new Line { LineNumber = word.Li };
                    block.Lines.Add(line);
                }
                if (line.Words == null)
                {
                    line.Words = new List<Word>();
                }
                line.Words.Add(word);


                // pop out of block if necessary
                var blockThatEndsOnThisWord = composedBlocks.FirstOrDefault(cb => cb.EndCharacter == wordKey);
                if (blockThatEndsOnThisWord != null)
                {
                    blockStack.Pop();
                }
            }
            return page;
        }
    }

}
