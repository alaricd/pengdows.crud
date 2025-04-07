// using System;
// using System.Data;
// using Microsoft.Extensions.DependencyInjection;
// using Xunit;
//
// namespace pengdows.crud.Tests;
//
// public class EntityHelperTests
// {
//     private readonly IServiceProvider _serviceProvider = new ServiceCollection().BuildServiceProvider();
//
//     [Fact]
//     public void Constructor_Throws_WhenTypeNotRegistered()
//     {
//         Assert.Throws<InvalidOperationException>(() =>
//             new EntityHelper<UnmappedEntity, Guid>(new FakeDatabaseContext(), _serviceProvider));
//     }
//
//     [Fact]
//     public void MakeParameterName_ReturnsCorrectMarker()
//     {
//         var ctx = new FakeDatabaseContext(":", false);
//         var helper = new EntityHelper<SampleEntity, Guid>(ctx, _serviceProvider);
//         var param = ctx.CreateDbParameter("name", DbType.String, "value");
//         Assert.Equal(":name", helper.MakeParameterName(param));
//     }
//
//     private class UnmappedEntity
//     {
//     }
// }