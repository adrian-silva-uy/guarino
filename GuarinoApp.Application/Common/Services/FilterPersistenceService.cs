using GuarinoApp.Application.Common.Filters;
using System.Text.Json;

namespace GuarinoApp.Application.Common.Services;
public static class FilterPersistenceService
    {
    public static string SerializeFilters(List<Filter> filters)
        {
        return JsonSerializer.Serialize(filters);
        }

    public static List<Filter> DeserializeFilters(string json)
        {
        if (string.IsNullOrEmpty(json)) return [];
        try
            {
            return JsonSerializer.Deserialize<List<Filter>>(json) ?? [];
            }
        catch
            {
            return [];
            }
        }
    }
