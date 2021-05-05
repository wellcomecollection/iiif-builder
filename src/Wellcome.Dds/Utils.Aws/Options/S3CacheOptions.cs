namespace Utils.Aws.Options
{
    public class S3CacheOptions
    {
        public bool WriteProtobuf { get; set; } = false;
        public bool ReadProtobuf { get; set; } = false;
        public bool WriteBinary { get; set; } = false;
    }
}