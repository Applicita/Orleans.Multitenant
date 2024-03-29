using OrleansMultitenant.Tests.Examples.AuthorizedStreaming;

namespace OrleansMultitenant.Tests.UnitTests;

[Collection(MultiPurposeCluster.Name)]
public class TenantIdTests
{
    readonly Orleans.TestingHost.TestCluster cluster;

    public static IEnumerable<object?[]> TenantKeyQualifiedKeys() => new object?[][] {
        new object?[] { ""       , ""      , "|"             },
        new object?[] { ""       , "2"     , "|2"            },
        new object?[] { ""       , "Key3"  , "|Key3"         },
        new object?[] { "A"      , ""      , "A|"            },
        new object?[] { "A"      , "1"     , "A|1"           },
        new object?[] { "TenantB", "Key2"  , "TenantB|Key2"  },
        new object?[] { "Te|antB", "Key2"  , "Te||antB|Key2" },
        new object?[] { "|"      , "Key4"  , "|||Key4"       },
        new object?[] { "||"     , "Key5"  , "|||||Key5"     },
        new object?[] { "C"      , "|Key6" , "C|~|Key6"      },
        new object?[] { "D"      , "~Key7" , "D|~~Key7"      },
        new object?[] { "E"      , "|~Key8", "E|~|~Key8"     }
    };

    public TenantIdTests(ClusterFixture fixture) => cluster = fixture.Cluster;

    [Theory]
    [MemberData(nameof(TenantKeyQualifiedKeys))]
    public void GetGrain_ForTenantWithKey_FormatsKeyCorrectly(string tenantId, string keyWithinTenant, string tenantQualifiedKey)
    {
        var grain = GetGrain(tenantId, keyWithinTenant);
        string key = grain.GetPrimaryKeyString();
        Assert.Equal(tenantQualifiedKey, key);
    }

    [Theory]
    [MemberData(nameof(TenantKeyQualifiedKeys))]
    [SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters", Justification = "Avoid code duplication")]
    public void GetTenantIdString_ForGrainWithTenant_ReturnsCorrectTenantId(string tenantId, string keyWithinTenant, string _)
    {
        var grain = GetGrain(tenantId, keyWithinTenant);
        string? roundtrippedTenantId = grain.GetTenantId();
        Assert.Equal(tenantId, roundtrippedTenantId);
    }

    [Theory]
    [MemberData(nameof(TenantKeyQualifiedKeys))]
    [SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters", Justification = "Avoid code duplication")]
    public void GetKeyWithinTenant_ForGrainWithTenant_ReturnsCorrectKeyWithinTenant(string tenantId, string keyWithinTenant, string _)
    {
        var grain = GetGrain(tenantId, keyWithinTenant);
        string roundtrippedKeyWithinTenant = grain.GetKeyWithinTenant();
        Assert.Equal(keyWithinTenant, roundtrippedKeyWithinTenant);
    }

    IGrainWithStringKey GetGrain(string tenantId, string keyWithinTenant)
    => cluster.Client.ForTenant(tenantId).GetGrain<IStreamProducerGrain>(keyWithinTenant);
}
