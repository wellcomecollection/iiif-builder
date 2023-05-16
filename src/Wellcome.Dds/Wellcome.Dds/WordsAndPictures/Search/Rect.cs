namespace Wellcome.Dds.WordsAndPictures.Search
{
    /// <summary>
    /// Search highlight for display in Player
    /// </summary>
    public class Rect
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int W { get; set; }
        public int H { get; set; }
        public int Hit { get; set; }
        public string? Before { get; set; }
        public string? Word { get; set; }
        public string? After { get; set; }
    }
}
