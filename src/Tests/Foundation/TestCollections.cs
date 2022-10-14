namespace OrleansMultitenant.Tests;

[CollectionDefinition(Name)]
public class MultiPurposeCluster : ICollectionFixture<ClusterFixture>
{
    public const string Name = nameof(MultiPurposeCluster);
}
