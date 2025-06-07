// using System;
// using Microsoft.Extensions.DependencyInjection;
// using Xunit;
//
// namespace pengdows.crud.Tests;
//
// public class AuditFieldResolverTests
// {
//     [Fact]
//     public void ResolveFrom_ReturnsDateTimeAndUser()
//     {
//         var services = new ServiceCollection();
//         services.AddSingleton<IAuditContextProvider<string>>(new TestAuditProvider());
//         var provider = services.BuildServiceProvider();
//         var afr = new AuditFieldResolver();
//         var (userId, now) = AuditFieldResolver.ResolveFrom(typeof(string), provider);
//
//         Assert.Equal("test-user", userId);
//         Assert.True(now > DateTime.MinValue);
//     }
// }