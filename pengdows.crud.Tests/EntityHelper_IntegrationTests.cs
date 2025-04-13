// using System;
// using System.Collections.Generic;
// using System.Threading.Tasks;
// using Xunit;
//
// namespace pengdows.crud.Tests;
//
// public class EntityHelper_IntegrationTests
// {
//     [Fact]
//     public async Task LoadListAsync_ReturnsAllMapped()
//     {
//         var helper = new EntityHelper<TestEntity, Guid>(new FakeDatabaseContext(), new FakeServiceProvider());
//         var sc = new FakeSqlContainer(new List<TestEntity>
//         {
//             new() { Id = Guid.NewGuid(), Name = "A" },
//             new() { Id = Guid.NewGuid(), Name = "B" }
//         });
//
//         var results = await helper.LoadListAsync(sc);
//
//         Assert.Equal(2, results.Count);
//         Assert.All(results, r => Assert.False(string.IsNullOrWhiteSpace(r.Name)));
//     }
//
//     private class TestEntity
//     {
//         public Guid Id { get; set; }
//         public string Name { get; set; }
//     }
// }

