using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace AgileContent.Itaas.E2E
{
    public static class JsonElementHelper
    {
        public static IEnumerable<JsonElement> GetJsonElementList(this JsonElement jsonElement, string property) =>
            jsonElement.TryGetProperty(property, out var result) ? result.EnumerateArray().ToList() : new List<JsonElement>();

        public static IEnumerable<JsonElement> GetListOfValuesIfPropertyExists(
            this IEnumerable<JsonElement> list,
            string property)
        {

            var ret = new List<JsonElement>();
            foreach (var element in list)
            {
                if (element.TryGetProperty(property, out var result))
                {
                    ret.Add(result);
                }
            }
            return ret;
        }
    }
}