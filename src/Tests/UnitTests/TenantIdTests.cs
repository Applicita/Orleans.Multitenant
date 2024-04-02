using OrleansMultitenant.Tests.Examples.AuthorizedStreaming;

namespace OrleansMultitenant.Tests.UnitTests;

[Collection(MultiPurposeCluster.Name)]
public class TenantIdTests(ClusterFixture fixture)
{
    readonly Orleans.TestingHost.TestCluster cluster = fixture.Cluster;

    public static TheoryData<string /*tenantId*/, string /*keyWithinTenant*/, string /*tenantQualifiedKey*/> TenantKeyQualifiedKeys() => new () { 
        { ""       , ""      , "|"            },
        { ""       , "2"     , "|2"           },
        { ""       , "Key3"  , "|Key3"        },
        { "A"      , ""      , "A|"           },
        { "A"      , "1"     , "A|1"          },
        { "TenantB", "Key2"  , "TenantB|Key2" },
        { "Te|antB", "Key2"  , "Te||antB|Key2"},
        { "|"      , "Key4"  , "|||Key4"      },
        { "||"     , "Key5"  , "|||||Key5"    },
        { "C"      , "|Key6" , "C|~|Key6"     },
        { "D"      , "~Key7" , "D|~~Key7"     },
        { "E"      , "|~Key8", "E|~|~Key8"    }
    };

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
