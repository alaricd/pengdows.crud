using System;
using Moq;
using Xunit;

namespace pengdows.crud.Tests
{
    public class IAuditContextProviderTests
    {
        [Fact]
        public void GetAuditStamp_ShouldIncludeUserAndTime()
        {
            var mock = new Mock<IAuditContextProvider<string>>();
            mock.Setup(m => m.GetCurrentUserIdentifier()).Returns("user-123");
            mock.Setup(m => m.GetUtcNow()).Returns(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            var logger = new AuditLogger(mock.Object);

            var result = logger.GetAuditStamp();

            Assert.Contains("user-123", result);
            Assert.Contains("2024-01-01T00:00:00.0000000Z", result);
        }


        public class AuditLogger
        {
            private readonly IAuditContextProvider<string> _context;

            public AuditLogger(IAuditContextProvider<string> context)
            {
                _context = context;
            }

            public string GetAuditStamp()
            {
                return $"{_context.GetCurrentUserIdentifier()} @ {_context.GetUtcNow():O}";
            }
        }
    }
}