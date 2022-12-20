using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Utils
{
    /// <summary>
    /// This class contains a number of utilities for processing HTML.
    /// 
    /// Some of the methods here parse HTML, to extract significant data.
    /// Some of the methods here manipulate existing HTML, to add or remove tags and attributes.
    /// 
    /// Why do we want to do this?
    /// 
    /// If web sites were left up to back-end developers alone, everything would be simple. All the HTML
    /// would be as simple as possible. In a CMS environment, editors could enter really simple rich text
    /// in the WYSIWYG editor and we could just take those fields and display them in the page.
    /// 
    /// The trouble with this is that the site would look rubbish - it would look like "my home page" circa 1995.
    /// 
    /// So we have designers, and User Experience gurus, and CSS mavens, to make the sites look beautiful.
    /// 
    /// We also want to follow a general principle - make the editors' lives as easy as possible.
    /// We don't want to expect the editors to have to know HTML. We really just want them to type without
    /// ever worrying about the HTML that they are generating in the background. This means its just plain p tags, 
    /// h tags, a tags and so on - no complex *structural* html, and no special requirements to put
    /// attributes on elements (e.g., a special css class for the last paragraph in a field). Morello
    /// gives us the ability for editors to add classes to elements - Morello can pick up style sheets
    /// stored in the special /stylesheets branch in the content store and make the classes available in 
    /// the style drop down. This is used in places, but it's still not ideal as it can be abused or
    /// forgotten about.
    /// 
    /// Now, in an ideal world we could still have the editorial users enter simple markup, and all the
    /// decoration is done by highly sophisticated CSS3. If the template structural markup starts first 
    /// and then evolves hand-in-hand with the CSS as the site is developed, this approach can be made to 
    /// work well - and this is usually the approach taken for new functionality with the web team.
    /// However, it doesn't work when the design comes first, then is implemented as HTML by someone else,
    /// then passed to the developers to incorporate into templates (the standard web development model in
    /// the 90s and early 2000s).
    /// 
    /// What usually happens is that some of the stuff that needs to come from editors, rather than from
    /// the template, still has some quite complex structural html requirements. One solution is to break it
    /// up into more fields on the type definitions, so that the markup can go back into the template. 
    /// But then you end up with too-complex type definitions with dozens of separate fields for individual
    /// fragments of text.
    /// 
    /// And even if you can work the HTML-first way, there will be browser compatibility issues (especially with IE6)
    /// that still require some HTML to have specific classes that will be fiddly for an editor to put in.
    /// 
    /// So what we need to do is have the facility for the editors to enter content as simply as possible, but
    /// have code to massage it and decorate it and enhance it before it gets emitted to the template. This
    /// gives a compromise - the type definitions don't get too complex, the template can do most of the
    /// structural or visual layout work, but the simple HTML that comes from item fields gets enhanced by
    /// adding in attributes or tags to make sure it's going to play well with the complex CSS that has
    /// been developed for the site.
    /// 
    /// These helper methods help resolve the conflict between complex template requirements to achieve a design,
    /// and simple html in markup fields to avoid confusing editors or making their job harder.
    /// 
    /// This class contains methods to do these various enhancements.
    /// 
    ///
    /// </summary>
    /// <example>
    /// <code>
    ///             
    /// string rawField = RequestItem.getFieldValue(Field);
    /// string processedField = HtmlUtils.StripEmptyParagraphTags(rawField);
    /// if (HtmlUtils.TextOnly(processedField).HasText())
    /// {
    ///     processedField = HtmlUtils.StripParagraphTagsAroundAnchorTags(processedField);
    ///     processedField = HtmlUtils.DecorateTagWithClass(processedField, "h2", "subh2", TagScope.First);
    ///     processedField = HtmlUtils.DecorateTagWithClass(processedField, "a", "rchev", TagScope.All);
    ///     processedField = HtmlUtils.DecorateTagWithClass(processedField, "li", "noline", TagScope.Last);
    ///     litProcessed.Text = processedField;
    /// }
    /// 
    /// </code>
    /// </example>
    public static class HtmlUtils
    {
        /// <summary>
        /// Given a chunk of HTML, find all the anchor tags and create Link objects from them.
        /// 
        /// Why?
        /// 
        /// This allows us to have a simple markup field on an item into which an editor can add an
        /// arbitrary number of links. The editor doesn't have to worry about formatting, or the correct
        /// html list markup to use, because we don't use their markup at all - we just extract the
        /// link information and end up we can bind to something in the template that we have complete control
        /// over.
        /// 
        /// <see cref="Link"/>
        /// </summary>
        /// <param name="s">An HTML string</param>
        /// <returns>A List of Link objects</returns>
        public static IList<Link> ParseLinks(string s)
        {
            var links = new List<Link>();
            if (s != null)
            {
                var re = new Regex("<a.*?href=\"([^\"]*)\"[^>]*>([^<]+)</a>", RegexOptions.IgnoreCase);
                var matches = re.Matches(s);
                foreach (Match match in matches)
                {
                    var link = new Link { Href = match.Groups[1].Value, Text = match.Groups[2].Value };
                    links.Add(link);
                }
            }
            return links;
        }

        /// <summary>
        /// Given a string of HTML, find all the tags of type tagName, and add a class attribute to them
        /// with the value className.
        /// 
        /// For example, find all the anchor tags and add class="myClass" to them.
        /// 
        /// Usually you would try to do this in CSS - if you're passing TagScope.All, you can almost
        /// certainly do what you're trying to acheive much more elegantly by CSS cascade from
        /// a containing element. Typically you'll be adding class="last" (or something like that) to the
        /// last tag in the chunk of Markup.
        /// 
        /// Does not work for single "closed" tags like img, br
        /// </summary>
        /// <param name="rawHtml">An HTML string</param>
        /// <param name="tagName">e.g., "a", "p", "h1" etc.</param>
        /// <param name="className">The class attribute value to add - can contain more than one class, separated by 
        /// spaces as per normal html. If the attribute already has the class, it won't be duplicated.</param>
        /// <param name="tagScope">Whether to add this to the first, last, or all occurences of the tag in the html</param>
        /// <returns></returns>
        public static string DecorateTagWithClass(string rawHtml, string tagName, string className, TagScope tagScope)
        {
            return AddSpacedAttributeValues(rawHtml, tagName, "class", className, tagScope);
        }

        /// <summary>
        /// Given a string of HTML, find all the tags of type tagName, and add a style attribute to them
        /// with the value inlineStyle.
        /// 
        /// For example, find all the anchor tags and add style="font-weight:bold" to them.
        ///
        /// Usually you would try to do this in CSS - if you're passing TagScope.All, you can almost
        /// certainly do what you're trying to acheive much more elegantly by CSS cascade from
        /// a containing element. 
        /// 
        /// Does not work for single "closed" tags like img, br
        /// </summary>
        /// <param name="rawHtml">An HTML string</param>
        /// <param name="tagName">e.g., "a", "p", "h1" etc.</param>
        /// <param name="inlineStyle">The style attribute value to add - can contain more than one declaration, 
        /// separated by spaces. If the attribute already has the declaration, it won't be duplicated.</param>
        /// <param name="tagScope">Whether to add this to the first, last, or all occurences of the tag in the html</param>
        /// <returns></returns>
        public static string DecorateTagWithStyle(string rawHtml, string tagName, string inlineStyle, TagScope tagScope)
        {
            return AddSpacedAttributeValues(rawHtml, tagName, "style", inlineStyle, tagScope);
        }

        /// <summary>
        /// Given a string of HTML, find all the tags of type tagName, and add an attribute (attributeName) to them
        /// with the value attributeValue. If the attribute is already present, attributeValue will be merged with the
        /// existing attribute value on the basis that the attribute value is a space separated list.
        /// 
        /// Examples (angle brackets omitted):
        /// 
        ///    p  =>  p attributeName="attributeValue"
        ///    p attributeName="existing1"  =>  p attributeName="existing1 attributeValue"
        ///    p attributeName="attributeValue"  =>  p attributeName="attributeValue"
        ///    p attributeName="existing1 existing2"  =>  p attributeName="existing1 existing2 attributeValue"
        ///    p attributeName="attributeValue existing1 existing2"  =>  p attributeName="attributeValue existing1 existing2"
        /// 
        /// </summary>
        /// <param name="rawHtml">An HTML string</param>
        /// <param name="tagName">e.g., "a", "p", "h1" etc.</param>
        /// <param name="attributeName">The name of the attribute to process</param>
        /// <param name="attributeValue">The attribute value to add - can contain more than one value, 
        /// separated by spaces. If the attribute already has the value, it won't be duplicated.</param>
        /// <param name="tagScope">Whether to add this to the first, last, or all occurences of the tag in the html</param>
        /// <returns></returns>
        public static string AddSpacedAttributeValues(string rawHtml, string tagName, string attributeName, string attributeValue, TagScope tagScope)
        {
            if (rawHtml.HasText())
            {
                string attributeSearchString = attributeName + "=\"";
                /*
                 * sample usage, explanantion of regex
                 * The positive lookahead is reqd to stop it matching <link..>
                 * <li(?=[ >])([^>]*)>

                    Match the characters "<li" literally «<li»
                    Assert that the regex below can be matched, starting at this position (positive lookahead) «(?=[ >])»
                       Match a single character present in the list " >" «[ >]»
                    Match the regular expression below and capture its match into backreference number 1 «([^>]*)»
                       Match any character that is not a ">" «[^>]*»
                          Between zero and unlimited times, as many times as possible, giving back as needed (greedy) «*»
                    Match the character ">" literally «>»
                 * 
                 * 
                 * Tip - use RegexBuddy.
                 */
                var re = new Regex("<" + tagName + "(?=[ >])([^>]*)>");
                var matches = re.Matches(rawHtml);
                if (matches.Count > 0)
                {
                    var sb = new StringBuilder();
                    int pos1 = 0;
                    for (int i = 0; i < matches.Count; i++)
                    {
                        if ((tagScope == TagScope.All)
                               || (tagScope == TagScope.First && i == 0)
                               || (tagScope == TagScope.Last && i == matches.Count - 1))
                        {
                            var match = matches[i];
                            sb.Append(rawHtml.Substring(pos1, match.Index - pos1));
                            pos1 = match.Index + match.Length;
                            string tagAttrs = matches[i].Groups[1].Value;
                            if (tagAttrs.HasText())
                            {
                                int atPos = tagAttrs.IndexOf(attributeSearchString, System.StringComparison.Ordinal);
                                if (atPos == -1)
                                {
                                    sb.Append("<");
                                    sb.Append(tagName);
                                    sb.Append(tagAttrs);
                                    sb.Append(" ");
                                    sb.Append(attributeSearchString);
                                    sb.Append(attributeValue);
                                    sb.Append("\">");
                                }
                                else
                                {
                                    // already a class attr; 
                                    var attrMatch = Regex.Match(tagAttrs, attributeSearchString + "([^\"]*)\"");
                                    string existingAttrs = attrMatch.Groups[1].Value;
                                    string newAttr = attributeValue;
                                    if (existingAttrs.HasText())
                                    {
                                        newAttr = existingAttrs + " " + attributeValue;
                                    }
                                    tagAttrs = tagAttrs.Replace(attrMatch.Value, attributeSearchString + newAttr + "\"");
                                    sb.Append("<");
                                    sb.Append(tagName);
                                    sb.Append(tagAttrs);
                                    sb.Append(">");
                                }
                            }
                            else
                            {
                                sb.Append("<");
                                sb.Append(tagName);
                                sb.Append(" ");
                                sb.Append(attributeSearchString);
                                sb.Append(attributeValue);
                                sb.Append("\">");
                            }
                        }
                    }
                    sb.Append(rawHtml.Substring(pos1));
                    return sb.ToString();
                }
            }
            return rawHtml;
        }

        /// <summary>
        /// Cleans up a string of HTML to remove any p tags that don't contain any content.
        /// So if an editor accidentally leaves a few carriage returns at the end of a field (which
        /// will be converted to p tags by the wysiwyg editor), extra white space won't appear in the
        /// finished page that uses that field.
        /// </summary>
        /// <param name="input">The HTML to clean</param>
        /// <returns></returns>
        public static string? StripEmptyParagraphTags(string? input)
        {
            if (input != null)
            {
                return Regex.Replace(input, @"<p>\s*</p>", "");
            }
            return null;
        }

        /// <summary>
        /// Cleans up a string of HTML to remove any p tags that don't contain any content, or ONLY
        /// contain forced nbsp; character escape sequences.
        /// So if an editor accidentally leaves a few carriage returns at the end of a field (which
        /// will be converted to p tags by the wysiwyg editor), extra white space won't appear in the
        /// finished page that uses that field.
        /// </summary>
        /// <param name="input">The HTML to clean</param>
        /// <returns></returns>
        public static string? StripEmptyOrNbspParagraphTags(string? input)
        {
            if (input != null)
            {
                return Regex.Replace(input, @"<p>(\s|(&nbsp;))*</p>", "");
            }
            return null;
        }

        /// <summary>
        /// Removes all tags from a string of HTML, leaving just the text content.
        /// 
        /// Text inside tag bodies is preserved.
        /// </summary>
        /// <param name="markup"></param>
        /// <returns></returns>
        public static string? TextOnly(string? markup)
        {
            if (markup != null)
            {
                return Regex.Replace(markup, "<(.|\\n)*?>", "");
            }
            return null;
        }

        public static string? TextOnlyWithSpaces(string? markup)
        {
            if (markup != null)
            {
                string s = Regex.Replace(markup, "<(.|\\n)*?>", " ");
                return StringUtils.NormaliseSpaces(s);
            }
            return null;
        }

        /// <summary>
        /// If an image tag is ONLY enclosed by p tags, remove the p tags
        /// 
        /// If an editor inserts an image into a block of text in a markup field, they will usually
        /// add some line breaks to make it look right - which will wrap the image in p tags.
        /// 
        /// Usually, the CSS won't be designed for that scenario and will be adding its own borders/margins
        /// to images in text.
        /// </summary>
        public static string? StripParagraphTagsAroundImages(string? input)
        {
            if (input != null)
            {
                return StripParagraphTagsAroundElement(input, @"<p>\s*(<img.*src=.*/>)\s*</p>");
            }
            return null;
        }

        /// <summary> 
        /// If an anchor tag is ONLY enclosed by p tags, remove the p tags
        /// 
        /// If an editor inserts a hyperlink on its own line into a block of text in a markup field, the
        /// wysiwyg editor will wrap the hyperlink in p tags.
        /// 
        /// This is fine in a long segment of body text but for some more tightly controlled designs
        /// (e.g., ImageCallout) the CSS isn't designed to style extra p tags. 
        /// </summary>
        public static string? StripParagraphTagsAroundAnchorTags(string? input)
        {
            if (input != null)
            {
                return StripParagraphTagsAroundElement(input, @"<p>\s*(<a.*href=.*</a>)\s*</p>");
            }
            return null;
        }

        /// <summary>
        /// Given a string of HTML, find elements that match the regex and remove any p tags that surround
        /// the element. This takes care of extraneous p tags introduced by editors using the wysiwyg editing
        /// tools in Morello.
        /// </summary>
        /// <param name="input">An HTML string</param>
        /// <param name="elementRegex">a Regular Expression that will match the HTML element</param>
        /// <returns></returns>
        public static string? StripParagraphTagsAroundElement(string? input, string elementRegex)
        {
            if (input != null)
            {
                // var newString = input.Replace("&nbsp;", "");
                var newString = input; // for testing
                var re = new Regex(elementRegex, RegexOptions.IgnoreCase);
                var matches = re.Matches(newString);
                foreach (Match match in matches)
                {
                    newString = newString.Replace(match.Value, match.Groups[1].Value);
                }
                return newString;
            }
            return null;
        }

        /// <summary>
        /// Extract an element's contents from a block of markup.
        /// 
        /// This is used in scenarios where we want the the editor to write something simple, but we want to put
        /// whatthey've written into a more complex HTML structure that they couldn't have made by themselves in 
        /// the Morello editor (unless they knew HTML and edited the source).
        /// 
        /// For example, we could use this to look for h tags, extract the contents, then do something else
        /// with that text in the template. This assists with the principle outlined in the class description
        /// <see cref="HtmlUtils"/>
        /// </summary>
        /// <param name="input">The markup block</param>
        /// <param name="element">The exact tag for the element</param>
        /// <returns>The content (without the tag)</returns>
        public static string ExtractFirstElementContentFromMarkup(string input, string element)
        {
            string result = string.Empty;
            if (input.HasText())
            {
                int tagStartPos = input.IndexOf(element, 0, System.StringComparison.Ordinal) + element.Length;
                int tagEndPos = input.IndexOf(element.Replace("<", "</"), System.StringComparison.Ordinal);
                if (tagEndPos != -1)
                    result = input.Substring(tagStartPos, tagEndPos - (element.Length + 1));
            }
            return result;
        }

        /// <summary>
        /// Takes a plain string (i.e., not HTML) and inserts break tags
        /// at every new line, so that the line breaks appear when the text is rendered in the browser.
        /// 
        /// This is for text that probably hasn't come from a markup field - which will already contain
        /// p or br tags. This is for plain text, e.g., from a string field or config file.
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string AddBreakTags(string s)
        {
            var lines = s.SplitByDelimiter('\n');
            if (lines == null) return s;

            var sb = new StringBuilder();
            bool first = true;
            foreach (string line in lines)
            {
                if (!first)
                    sb.Append("<br/>");
                sb.AppendLine(line);
                first = false;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Returns the supplied raw string inside a p tag if no p tag already exists.
        /// If the supplied text contains any p tag anywhere, it will be returned unchanged.
        /// 
        /// Often, template CSS will expect the text in a block to be wrapped in p tags.
        /// But in the Morello editor, if you just type one line into an emtpy markup field
        /// and save it without entering a line break, the markup won't contain any enclosing p
        /// tags because you haven't inserted a carriage return (which is the correct behaviour).
        /// 
        /// This method ensures that markup always contains at least one set of p tags.
        /// </summary>
        /// <param name="raw">A string of html (or plain text)</param>
        /// <returns>html with at least one p tag - i.e., no "free-standing" text</returns>
        public static string? EnsureParagraphTags(string? raw)
        {
            if (raw == null) return null;
            if (raw.IndexOf("<p", System.StringComparison.Ordinal) == -1)
            {
                return "<p>" + raw + "</p>";
            }
            return raw;
        }

        /// <summary>
        /// If for some reason the edit mode is set to insert br tags, or the editor has inserted line
        /// breaks with shift+return to simulate paras, this cleans it up so that the text is split
        /// with real p tags.
        /// </summary>
        /// <param name="raw"></param>
        /// <returns></returns>
        public static string? ConvertDoubleBreakTagsToParas(string? raw)
        {
            // don't do this if the supplied string already contains paragraphs
            if (raw == null) return null;
            if (raw.ToLowerInvariant().IndexOf("<p", System.StringComparison.Ordinal) == -1)
            {
                var parts = Regex.Split(raw, " *<br */* *> *<br */* *> *", RegexOptions.IgnoreCase);
                var sb = new StringBuilder();
                foreach (string part in parts)
                {
                    sb.Append("\r\n<p>\r\n");
                    sb.Append(part);
                    sb.Append("\r\n</p>\r\n");
                }
                return sb.ToString();
            }
            return raw;
        }

        /// <summary>
        /// Converts all h1, h2, h3, ... h6 tags in the supplied markup to the tag supplied
        /// 
        /// We can't always rely on an editor to use the right heading level. But when we're inserting
        /// a chunk of edited html in a template we ususally know what h level is required, and we can use
        /// this method to make sure all headings in the block of html are a particular level.
        /// </summary>
        /// <param name="markup">A string of html</param>
        /// <param name="requiredTag">e.g., "h3"</param>
        /// <returns></returns>
        public static string NormaliseHeadings(string markup, string requiredTag)
        {
            return Regex.Replace(markup, "<(.?)h[1-9]>", "<$1" + requiredTag + ">");
        }

        /// <summary>
        /// Converts all h1, h2, h3, ... h6 tags in the supplied markup to the tag supplied
        /// 
        /// We can't always rely on an editor to use the right heading level. But when we're inserting
        /// a chunk of edited html in a template we ususally know what h level is required, and we can use
        /// this method to make sure all headings in the block of html are a particular level.
        /// </summary>
        /// <param name="markup">A string of html</param>
        /// <param name="requiredTag">e.g., "h3"</param>
        /// <param name="headingLevel">e.g., 4: h5 and h6 will be left alone.</param>
        /// <returns></returns>
        public static string NormaliseHeadingsUpTo(string markup, string requiredTag, int headingLevel)
        {
            return Regex.Replace(markup, "<(.?)h[1-" + headingLevel + "]>", "<$1" + requiredTag + ">");
        }

        public static void AppendHtmlHeading(this StringBuilder sb, int level, string heading)
        {
            sb.AppendFormat("<h{0}>{1}</h{0}>", level, heading);
        }

        public static void AppendLine(this StringBuilder sb, int numLines)
        {
            for (int i = 0; i < numLines; i++)
                sb.AppendLine();
        }

    }

    /// <summary>
    /// Used by methods that look for particular tags in a chunk of HTML, and alter them in some way. 
    /// This enum allows us to specify which tag(s) in the chunk to apply the desired changes to.
    /// </summary>
    public enum TagScope
    {
        First,
        Last,
        All
    }


}
