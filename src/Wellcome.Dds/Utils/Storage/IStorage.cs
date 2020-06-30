namespace Utils.Storage
{
    public interface IStorage
    {
        // This might be a bucket, or a filesystem location
        public string Folder { get; set; }

        ISimpleStoredFileInfo GetCachedFile(string fileName);
        void DeleteCacheFile(string fileName);
        void Write<T>(T t, ISimpleStoredFileInfo fileInfo, bool writeFailThrowsException) where T : class;
        T Read<T>(ISimpleStoredFileInfo fileInfo) where T : class;
    }
}
