namespace IIIF.Presentation.Annotation
{
    public class Annotation : ResourceBase, IAnnotation
    {
        public override string Type => nameof(Annotation);
        
        /// <summary>
        /// A mode associated with an Annotation that is to be applied to the rendering of any time-based media.
        /// see <a href="https://iiif.io/api/presentation/3.0/#timemode">timemode</a>
        /// </summary>
        public string? TimeMode { get; set; }
        public virtual string? Motivation { get; set; }
    }
}
