namespace Wellcome.Dds.Repositories.Presentation.SpecialState
{
    /// <summary>
    /// This is a kind of memo... probably can use something that already exists, not this
    /// </summary>
    public class MultipleManifestationMember
    {
        public MultipleManifestationMember(string id, string type)
        {
            Id = id;
            Type = type;
        }
        public string Id { get; set; }
        public string Type { get; set; }
    }
}