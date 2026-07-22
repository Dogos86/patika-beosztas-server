using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PatikaBeosztas.Domain;

namespace PatikaBeosztas.Api.IntegrationTests;

[TestClass]
public sealed class LeaveJsonContractTests
{
    [TestMethod]
    public void AllPublicLeaveEnumsRoundTripAsDocumentedStrings()
    {
        AssertStringRoundTrip(Enum.GetValues<LeaveType>());
        AssertStringRoundTrip(Enum.GetValues<LeaveRequestStatus>());
        AssertStringRoundTrip(Enum.GetValues<LeaveDecision>());
    }

    private static void AssertStringRoundTrip<TEnum>(IEnumerable<TEnum> values)
        where TEnum : struct, Enum
    {
        foreach (var value in values)
        {
            var json = JsonSerializer.Serialize(value, IntegrationJson.Options);
            Assert.AreEqual($"\"{value}\"", json);
            Assert.AreEqual(
                value,
                JsonSerializer.Deserialize<TEnum>(json, IntegrationJson.Options));
        }
    }
}
