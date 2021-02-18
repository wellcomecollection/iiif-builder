namespace IIIF
{
    public class ServiceReference : IService
    {
        public string? Id { get; set; }
        public string Type { get; set; }

        public ServiceReference(string type)
        {
            Type = type;
        }

        public ServiceReference(IService service)
        {
            Id = service.Id;
            Type = service.Type;
        }
    }
}