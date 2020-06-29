namespace Wellcome.Dds.AssetDomain
{
    public interface IWorkStorageFactory
    {
        IWorkStore GetWorkStore(string identifier);
    }
}
