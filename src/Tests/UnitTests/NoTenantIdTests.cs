using OrleansMultitenant.Tests.Examples.AuthorizedStreaming;

namespace OrleansMultitenant.Tests.UnitTests;

[Collection(MultiPurposeCluster.Name)]
public class NoTenantIdTests
{
    readonly Orleans.TestingHost.TestCluster cluster;

    public static IEnumerable<object?[]> KeyQualifiedKeys() => new object?[][] {
        new object?[] { ""      , ""       },
        new object?[] { "1"     , "1"      },
        new object?[] { "Key2"  , "Key2"   },
        new object?[] { "|"     , "||"     },
        new object?[] { "||"    , "||||"   },
        new object?[] { "|Key3" , "||Key3" },
        new object?[] { "K|ey4" , "K||ey4" },
        new object?[] { "Key5|" , "Key5||" },
        new object?[] { "~Key6" , "~Key6"  },
        new object?[] { "|~Key7", "||~Key7"}
    };

    public NoTenantIdTests(ClusterFixture fixture) => cluster = fixture.Cluster;

    [Theory]
    [MemberData(nameof(KeyQualifiedKeys))]
    public void GetGrain_ForNoTenantWithKey_FormatsKeyCorrectly(string key, string tenantQualifiedKey)
    {
        var grain = GetGrain(key);
        string roundtrippedKey = grain.GetPrimaryKeyString();
        Assert.Equal(tenantQualifiedKey, roundtrippedKey);
    }

    [Theory]
    [MemberData(nameof(KeyQualifiedKeys))]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters", Justification = "Avoid code duplication")]
    public void GetTenantIdString_ForGrainWithNoTenant_ReturnsNull(string key, string _)
    {
        var grain = GetGrain(key);
        string? roundtrippedTenantId = grain.GetTenantId();
        Assert.Null(roundtrippedTenantId);
    }

    [Theory]
    [MemberData(nameof(KeyQualifiedKeys))]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters", Justification = "Avoid code duplication")]
    public void GetKeyWithinTenant_ForGrainWithTenant_ReturnsCorrectKeyWithinTenant(string key, string _)
    {
        var grain = GetGrain(key);
        string roundtrippedKeyWithinTenant = grain.GetKeyWithinTenant();
        Assert.Equal(key, roundtrippedKeyWithinTenant);
    }

    IGrainWithStringKey GetGrain(string key)
    => cluster.Client.ForTenant(null!).GetGrain<IStreamProducerGrain>(key);
}
