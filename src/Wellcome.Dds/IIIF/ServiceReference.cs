namespace IIIF
{
    public class ServiceReference : IService
    {
        public string? Id { get; set; }
        public string? Type { get; set; }

        public ServiceReference()
        {
        }

        public ServiceReference(IService service)
        {
            Id = service.Id;
            Type = service.Type;
        }
    }
}