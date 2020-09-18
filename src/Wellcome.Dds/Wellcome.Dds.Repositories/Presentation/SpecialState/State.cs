namespace Wellcome.Dds.Repositories.Presentation.SpecialState
{
    /// <summary>
    /// Wherever possible, we build things independently of their position in a tree.
    /// We can build one volume of a multi volume work without having to build them all.
    /// This isn't always possible, we can't just walk up to an AV manifestation
    /// and build it independently. We don't have enough info.
    /// 
    /// An AV "Collection" may end up having to be re-arranged into a single Manifest, if
    /// it's a multiple manifestation comprising one video and one PDF transcript.
    /// 
    /// Sometimes, we have to build all the things, then go back and rearrange.
    /// This class holds state that allows us to fix things up at the end.
    ///
    /// For C&D, we can, now, walk up to and build an individual issue without having to
    /// build the whole tree. Hooray!
    /// But we can't build the root b19974760 C&D collection without
    /// diving into the tree for more info, because the root b19974760 collection
    /// has data that only lives at the Volume and Issue level METS.
    /// </summary>
    public class State
    {
        public MultiCopyState MultiCopyState { get; set; }
        public AVState AVState { get; set; }
        public ChemistAndDruggistState ChemistAndDruggistState { get; set; }

        // which one of these to use...
        public bool NeedsInfoFromChildren { get; set; }
        
        public bool HasState => MultiCopyState != null || AVState != null || ChemistAndDruggistState != null;
    }
}