﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Utils;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.Common;
using Wellcome.Dds.WordsAndPictures;
using Image = Wellcome.Dds.WordsAndPictures.Image;

namespace Wellcome.Dds.Repositories.WordsAndPictures
{
    public class AltoSearchTextProvider : ISearchTextProvider
    {
        //private readonly IDipProvider dipProvider;
        private readonly IMetsRepository metsRepository;
        private readonly ILogger<AltoSearchTextProvider> logger;
        private static readonly XNamespace Ns = "http://www.loc.gov/standards/alto/ns-v2#";
        IIdentityService identityService;

        public AltoSearchTextProvider(
            IMetsRepository metsRepository,
            ILogger<AltoSearchTextProvider> logger,
            IIdentityService identityService)
        {
            this.metsRepository = metsRepository;
            this.logger = logger;
            this.identityService = identityService;
        }

        public async Task<Text?> GetSearchText(string identifier)
        {
            var sw = new Stopwatch();
            sw.Start();
            logger.LogInformation("Getting search text from ALTO files for {Identifier}", identifier);
            var ddsId = identityService.GetIdentity(identifier);
            var manifestation = await metsRepository.GetAsync(ddsId) as IManifestation;
            if (manifestation == null)
            {
                return null;
            }
            var words = new Dictionary<int, Word>();
            var buckets = new Dictionary<string, HashSet<string>>();
            var images = new List<Image>();
            var composedBlocks = new Dictionary<string, ComposedBlock>();
            var sbNorm = new StringBuilder();
            var sbRaw = new StringBuilder();
            int positionNorm = 0;
            int positionRaw = 0;
            int lineCounter = 0;
            int wordCounter = 0;
            int composedBlockCounter = 0;
            for (int assetIndex = 0; assetIndex < manifestation.Sequence!.Count; assetIndex++)
            {
                // TODO - logging, errors
                var physicalFile = manifestation.Sequence[assetIndex];
                images.Add(new Image
                    {
                        StartCharacter = positionNorm,
                        ImageIdentifier = physicalFile.StorageIdentifier
                    });
                if (physicalFile.RelativeAltoPath.HasText())
                {
                    logger.LogInformation("Attempting to load ALTO: {RelativeAltoPath}", physicalFile.RelativeAltoPath);
                    try
                    {
                        var pathXml = await physicalFile.WorkStore.LoadXmlForPath(physicalFile.RelativeAltoPath, false);
                        var altoRoot = pathXml.XElement;
                        if (altoRoot == null)
                        {
                            logger.LogWarning("No ALTO element in {RelativeAltoPath}, skipping", physicalFile.RelativeAltoPath);
                            continue;
                        }
                        var pageElement = altoRoot.Element(Ns + "Layout")?.Element(Ns + "Page");
                        if (pageElement == null)
                        {
                            logger.LogWarning("No PAGE element in {RelativeAltoPath}, skipping", physicalFile.RelativeAltoPath);
                            continue;
                        }
                        int srcW = Convert.ToInt32(pageElement.GetRequiredAttributeValue("WIDTH"));
                        int srcH = Convert.ToInt32(pageElement.GetRequiredAttributeValue("HEIGHT"));
                        float scaleW = (float) physicalFile.AssetMetadata!.GetImageWidth() / srcW;
                        float scaleH = (float) physicalFile.AssetMetadata.GetImageHeight() / srcH;
                        // only get strings in text blocks, not page numbers and headers
                        var printSpace = altoRoot.Descendants(Ns + "PrintSpace").First();

                        foreach (var textBlock in printSpace.Descendants(Ns + "TextBlock"))
                        {
                            // test to see if this textBlock contains JUST a page number and nothing else
                            // This doesn't really work because sometimes page number is just on a textline not a sep block.
                            // look at Biocrats. Needs more work.
                            // IF alto file has no margin elements then first lone string that is a number or last lone string that is a number
                            // could be ignored. What to do about running titles?

                            // comment this out for now as it really should be fixed by providing better ALTO files that have proper PrintSpace,
                            // not trying to outguess the typesetter.
                            // var strings = textBlock.Descendants(ns + "String").ToList();
                            // if (strings.Count() == 1)
                            // {
                            //    var ps = strings[0].GetRequiredAttributeValue("CONTENT").Trim();
                            //    int pageNum;
                            //    if (int.TryParse(ps, out pageNum))
                            //    {
                            //        continue;
                            //    }
                            //    if (StringUtils.IsRomanNumeral(ps))
                            //    {
                            //        continue;
                            //    }
                            // }

                            
                            Word? prevWord = null;
                            
                            bool wordIsHyphenSecondPart = false;
                            foreach (var textLine in textBlock.Descendants(Ns + "TextLine"))
                            {
                                var line = lineCounter++;
                                // if a word is hyphenated we'll have to keep it simple and just regard the 
                                // first part as the full word. Otherwise a word would have to have two rectangles.
                                foreach (var xString in textLine.Descendants(Ns + "String"))
                                {
                                    const string hyphenSpecial = "¬";
                                    bool wordIsHyphenFirstPart = false;
                                    var rawWord = xString.GetRequiredAttributeValue("CONTENT");
                                    var subTypeAttr = xString.Attribute("SUBS_TYPE");
                                    if (subTypeAttr != null && subTypeAttr.Value == "HypPart1")
                                    {
                                        wordIsHyphenFirstPart = true;
                                    }
                                    if (rawWord.EndsWith(hyphenSpecial))
                                    {
                                        wordIsHyphenFirstPart = true;
                                        rawWord = rawWord.Chomp(hyphenSpecial);
                                    }

                                    var normalisedWord = Text.Normalise(rawWord);
                                    var word = new Word(rawWord, normalisedWord)
                                    {
                                        Idx = assetIndex,
                                        Li = line,
                                        Wd = wordCounter,
                                        X = (int)(Convert.ToInt32(xString.GetRequiredAttributeValue("HPOS")) * scaleW),
                                        Y = (int)(Convert.ToInt32(xString.GetRequiredAttributeValue("VPOS")) * scaleH),
                                        W = (int)(Convert.ToInt32(xString.GetRequiredAttributeValue("WIDTH")) * scaleW),
                                        H = (int)(Convert.ToInt32(xString.GetRequiredAttributeValue("HEIGHT")) * scaleH)
                                    };
                                    if (wordIsHyphenSecondPart && prevWord != null)
                                    {
                                        // we need to finish off the word from the previous line
                                        // this string element is not a new word
                                        var subsContentAttr = xString.Attribute("SUBS_CONTENT");
                                        if (subsContentAttr != null)
                                        {
                                            rawWord = subsContentAttr.Value;
                                        }
                                        else
                                        {
                                            rawWord = prevWord.ToRawString() + word.ToRawString();
                                        }
                                        prevWord.ContentRaw = rawWord;
                                        normalisedWord = Text.Normalise(rawWord);
                                        prevWord.ContentNorm = normalisedWord;
                                        word = prevWord;
                                        prevWord = null;
                                        wordIsHyphenSecondPart = false;
                                    }
                                    else
                                    {
                                        wordCounter++;
                                        // is there a space following this word?
                                        var spaceElement = xString.ElementsAfterSelf(Ns + "SP").FirstOrDefault();
                                        if (spaceElement != null)
                                        {
                                            word.Sp = (int) (Convert.ToInt32(spaceElement.GetRequiredAttributeValue("WIDTH"))*scaleW);
                                        }
                                        word.PosNorm = positionNorm;
                                        word.PosRaw = positionRaw;
                                        words[positionNorm] = word;
                                        prevWord = word;
                                    }
                                    if (wordIsHyphenFirstPart)
                                    {
                                        wordIsHyphenSecondPart = true;
                                    }
                                    else
                                    {
                                        FinaliseWord(word, sbNorm, sbRaw, ref positionNorm, ref positionRaw);
                                        AddToAutoCompleteSuggestions(normalisedWord, buckets);
                                        UpdateComposedBlocks(textBlock, composedBlocks, assetIndex, word, ref composedBlockCounter);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Cannot read or parse ALTO in {relativeAltoPath}", physicalFile.RelativeAltoPath);
                    }
                }
            }

            var normalisedText = sbNorm.ToString().TrimEnd();
            var rawText = sbRaw.ToString().TrimEnd();
            logger.LogInformation("Raw Text: {0} bytes", rawText.Length);
            logger.LogInformation("Norm Text: {0} bytes", normalisedText.Length);
            logger.LogInformation("Words: {0}", words.Count);
            var text = new Text
                {
                    NormalisedFullText = normalisedText, 
                    RawFullText = rawText,
                    Words = words, 
                    Images = images.ToArray(),
                    ComposedBlocks = composedBlocks.Values.OrderBy(cb => cb.ComposedBlockIndex).ToArray(),
                    AutoCompleteBuckets = buckets
                };
            sw.Stop();
            logger.LogInformation($"Text for {identifier} built in {sw.ElapsedMilliseconds} ms ({words.Count} words).");
            return text;
        }

        private static void UpdateComposedBlocks(XElement textBlock, Dictionary<string, ComposedBlock> composedBlocks, 
            int fileIndex, Word word, ref int composedBlockCounter)
        {
            // build up a map of the composed blocks as we go, we keep on updating the end position as we progress.
            // this should support nested composed blocks (if there are any)
            var composedBlockElement = textBlock.Ancestors(Ns + "ComposedBlock").FirstOrDefault();
            if (composedBlockElement != null)
            {
                var cbId = composedBlockElement.GetRequiredAttributeValue("ID");
                var dictKey = "" + fileIndex + "_" + cbId;
                ComposedBlock currentCb;
                if (!composedBlocks.ContainsKey(dictKey))
                {
                    currentCb = new ComposedBlock
                        {
                            ImageIndex = fileIndex,
                            StartCharacter = word.PosNorm,
                            EndCharacter = word.PosNorm,
                            ComposedBlockIndex = composedBlockCounter++
                        };
                    composedBlocks.Add(dictKey, currentCb);
                }
                else
                {
                    currentCb = composedBlocks[dictKey];
                    currentCb.EndCharacter = word.PosNorm; // keep on updating this while we're in the block
                }
            }
        }

        private static void FinaliseWord(Word word, StringBuilder sbNorm, StringBuilder sbRaw, ref int positionNorm,
                                         ref int positionRaw)
        {
            var forNormText = word + " ";
            var forRawText = word.ToRawString() + " ";
            sbNorm.Append(forNormText);
            sbRaw.Append(forRawText);
            positionNorm += forNormText.Length;
            positionRaw += forRawText.Length;
        }

        private static void AddToAutoCompleteSuggestions(string normalisedWord, Dictionary<string, HashSet<string>> buckets)
        {
            if (normalisedWord.Length > 2)
            {
                var key = normalisedWord.Substring(0, 3);
                if (!buckets.ContainsKey(key))
                {
                    buckets[key] = new HashSet<string>();
                }
                var suggestions = buckets[key];
                if (!suggestions.Contains(normalisedWord))
                {
                    suggestions.Add(normalisedWord);
                }
            }
        }
    }
}
