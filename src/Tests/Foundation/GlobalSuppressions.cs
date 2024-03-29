// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

[assembly: SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "https://docs.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices#naming-your-tests", Scope = "namespaceanddescendants", Target = "~N:OrleansMultitenant.Tests.UnitTests")]
[assembly: SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "Not relevant for tests", Scope = "namespaceanddescendants", Target = "~N:OrleansMultitenant.Tests")]
