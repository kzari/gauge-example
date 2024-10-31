using System.Collections.Generic;

namespace AgileContent.Itaas.E2E.Models;

public class CatalogLocation
{
    public string LocationType { get; set; }
    public string Bucket { get; set; }
    public string Key { get; set; }
    public IEnumerable<string> Keys { get; set; }
}