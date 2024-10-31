using AgileContent.Itaas.E2E.Models;

namespace E2E.Models;
public class CatalogRequestBody {
    public CatalogRequestBody() {
        CatalogLocation = new CatalogLocation();
        ServerUrls = new ServerUrls();
    }

    public string InstanceId { get; set; }
    public string Name { get; set; }
    public string Language { get; set; }
    public int CacheDuration { get; set; }
    public bool AutoActivate { get; set; }
    public ServerUrls ServerUrls { get; set; }
    public CatalogLocation CatalogLocation { get; set; }
}