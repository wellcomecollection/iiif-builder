namespace Utils.Logging
{
    public interface ISimpleLogger
    {
        void LogFormat(string format, params object[] args);
        void Log(string message);
    }
}
