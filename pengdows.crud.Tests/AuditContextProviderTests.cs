using System;
using Xunit;

namespace pengdows.crud.Tests
{
    public class AuditContextProviderTests
    {
        [Fact]
        public void GetUtcNow_ShouldReturnUtcTime()
        {
            // Arrange
            var provider = new TestAuditContextProvider();

            // Act
            DateTime now = provider.GetUtcNow();

            // Assert
            Assert.True(now.Kind == DateTimeKind.Utc);
            Assert.True((DateTime.UtcNow - now).TotalSeconds < 1); // within 1 second
        }

        [Fact]
        public void GetCurrentUserIdentifier_ShouldReturnTestValue()
        {
            var provider = new TestAuditContextProvider();
            var result = provider.GetCurrentUserIdentifier();
            Assert.Equal("test-user", result);
        }
        
        [Fact]
        public void GetCurrentUserIdentifierInt_ShouldReturnTestValue()
        {
            var provider = new TestIntAuditContextProvider();
            var result = provider.GetCurrentUserIdentifier();
            Assert.Equal(1, result);
        }
        
        public class TestAuditContextProvider : AuditContextProvider<string>
        {
            public override string GetCurrentUserIdentifier()
            {
                return "test-user";
            }
        }        
        
        public class TestIntAuditContextProvider : AuditContextProvider<int>
        {
            public override int GetCurrentUserIdentifier()
            {
                return 1;
            }
        }

    }
}