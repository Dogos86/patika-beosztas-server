using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PatikaBeosztas.Api.IntegrationTests;

internal static class IntegrationJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static async Task<T> ReadSuccessAsync<T>(HttpResponseMessage response)
    {
        var rawJson = await response.Content.ReadAsStringAsync();
        Assert.IsTrue(
            response.IsSuccessStatusCode,
            $"Váratlan HTTP {(int)response.StatusCode} ({response.StatusCode}). Törzs: {rawJson}");

        try
        {
            var result = JsonSerializer.Deserialize<T>(rawJson, Options);
            Assert.IsNotNull(result, $"A sikeres válasz törzse null. Törzs: {rawJson}");
            return result;
        }
        catch (JsonException exception)
        {
            Assert.Fail($"A sikeres válasz nem felel meg a contractnak: {exception.Message} Törzs: {rawJson}");
            throw;
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        return options;
    }
}
