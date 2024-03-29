// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Naming", "CA1721:Property names should not match get methods", Justification = "Type is for tool access only", Scope = "type", Target = "~T:Orleans4Multitenant.Apis.TenantApi.TenantController")]
[assembly: SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "Not relevant for ASP.NET Core applications", Scope = "namespaceanddescendants", Target = "~N:Orleans4Multitenant.Apis")]
