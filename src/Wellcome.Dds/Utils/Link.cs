using System;
using System.Text;

namespace Utils
{
    /// <summary>
    /// Represention of the properties of an anchor tag (hyperlink element) - its attributes and inner text.
    /// 
    /// This is useful in databinding scenarios, and in manipulating HTML.
    /// 
    /// Do not add any properties to this class that do not directly represent attributes of a link element
    /// This represents a hyperlink HTML element (anchor tag), it is NOT a lightweight IItem 
    /// 
    /// It is also not an alternative to System.Web.UI.WebControls.HyperLink, 
    /// or System.Web.UI.HtmlControls.HtmlAnchor - they represent actual controls, whereas this class
    /// just represents the data. You might databind a Link to a HyperLink or HtmlAnchor.
    /// </summary>
    [Serializable]
    public class Link : IComparable<Link>
    {
        public string Href { get; set; }
        public string Text { get; set; }
        public string CssClass { get; set; }
        public string Rel { get; set; }
        public string Title { get; set; }

        public int CompareTo(Link other)
        {
            return String.CompareOrdinal(Text, other.Text);
        }

        public override string ToString()
        {
            return String.Format("[{0} - {1}]", Href, Text);
        }

        public string ToHtml()
        {
            var sb = new StringBuilder();
            if (Href.HasText())
            {
                sb.AppendFormat("<a href=\"{0}\"", Href);
                if (Title.HasText())
                {
                    sb.AppendFormat(" title=\"{0}\"", Title);
                }
                if (Rel.HasText())
                {
                    sb.AppendFormat(" rel=\"{0}\"", Rel);
                }
                if (CssClass.HasText())
                {
                    sb.AppendFormat(" class=\"{0}\"", CssClass);
                }
                sb.Append(">");
            }
            sb.Append(Text);
            if (Href.HasText())
            {
                sb.Append("</a>");
            }
            return sb.ToString();
        }
    }
}
