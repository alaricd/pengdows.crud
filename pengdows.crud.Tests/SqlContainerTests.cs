namespace Pengdows.Crud.Tests;

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Moq;
using Xunit;
using pengdows.crud;


    public class SqlContainerTests
    {
        private Mock<IDatabaseContext> _mockContext;

        public SqlContainerTests()
        {
            _mockContext = new Mock<IDatabaseContext>();
            _mockContext.Setup(c => c.CreateDbParameter(It.IsAny<string>(), It.IsAny<DbType>(), It.IsAny<object?>()))
                .Returns((string name, DbType type, object? value) => new Mock<DbParameter>().Object);
        }

        [Fact]
        public void Constructor_InitializesQuery()
        {
            // Arrange & Act
            var container = new SqlContainer(_mockContext.Object);

            // Assert
            Assert.NotNull(container.Query);
        }

        [Fact]
        public void AppendParameter_AddsParameterToList()
        {
            // Arrange
            var container = new SqlContainer(_mockContext.Object);

            // Act
            var param = container.AppendParameter("param1", DbType.String, "value");

            // Assert
            Assert.Single(container.Query);
            Assert.Equal("param1", param.ParameterName);
        }

        [Fact]
        public async Task ExecuteNonQueryAsync_ExecutesQuerySuccessfully()
        {
            // Arrange
            var container = new SqlContainer(_mockContext.Object);
            _mockContext.Setup(c => c.GetConnection(It.IsAny<ExecutionType>())).Returns(new Mock<DbConnection>().Object);
            var mockCmd = new Mock<DbCommand>();
            mockCmd.Setup(m => m.ExecuteNonQueryAsync()).ReturnsAsync(1);
            _mockContext.Setup(c => c.CreateCommand()).Returns(mockCmd.Object);

            // Act
            var result = await container.ExecuteNonQueryAsync();

            // Assert
            Assert.Equal(1, result);
            mockCmd.Verify(cmd => cmd.ExecuteNonQueryAsync(), Times.Once);
        }

        [Fact]
        public async Task ExecuteScalarAsync_ReturnsValueSuccessfully()
        {
            // Arrange
            var container = new SqlContainer(_mockContext.Object);
            var mockReader = new Mock<DbDataReader>();
            mockReader.Setup(r => r.ReadAsync()).ReturnsAsync(true);
            mockReader.Setup(r => r.GetValue(0)).Returns("test_value");

            var mockCmd = new Mock<DbCommand>();
            mockCmd.Setup(m => m.ExecuteReaderAsync(It.IsAny<CommandBehavior>())).ReturnsAsync(mockReader.Object);
            _mockContext.Setup(c => c.CreateCommand()).Returns(mockCmd.Object);

            // Act
            var result = await container.ExecuteScalarAsync<string>();

            // Assert
            Assert.Equal("test_value", result);
            mockCmd.Verify(cmd => cmd.ExecuteReaderAsync(It.IsAny<CommandBehavior>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteReaderAsync_ReturnsReaderSuccessfully()
        {
            // Arrange
            var container = new SqlContainer(_mockContext.Object);
            var mockReader = new Mock<DbDataReader>();
            mockReader.Setup(r => r.ReadAsync()).ReturnsAsync(true);

            var mockCmd = new Mock<DbCommand>();
            mockCmd.Setup(m => m.ExecuteReaderAsync(It.IsAny<CommandBehavior>())).ReturnsAsync(mockReader.Object);
            _mockContext.Setup(c => c.CreateCommand()).Returns(mockCmd.Object);

            // Act
            var reader = await container.ExecuteReaderAsync();

            // Assert
            Assert.NotNull(reader);
            mockCmd.Verify(cmd => cmd.ExecuteReaderAsync(It.IsAny<CommandBehavior>()), Times.Once);
        }

        [Fact]
        public void PrepareCommand_ThrowsWhenTooManyParameters()
        {
            // Arrange
            var container = new SqlContainer(_mockContext.Object);
            _mockContext.Setup(c => c.MaxParameterLimit).Returns(2);
            container.AppendParameter("param1", DbType.Int32, 1);
            container.AppendParameter("param2", DbType.Int32, 2);
            container.AppendParameter("param3", DbType.Int32, 3); // Exceeding limit

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => container.ExecuteNonQueryAsync().Wait());
            Assert.Contains("Query exceeds the maximum parameter limit", exception.Message);
        }

        [Fact]
        public void WrapForStoredProc_HandlesDifferentDatabaseTypes()
        {
            // Arrange
            var container = new SqlContainer(_mockContext.Object);
            _mockContext.Setup(c => c.ProcWrappingStyle).Returns(ProcWrappingStyle.PostgreSQL);

            // Act
            var result = container.WrapForStoredProc(ExecutionType.Read);

            // Assert
            Assert.StartsWith("SELECT * FROM", result);

            _mockContext.Setup(c => c.ProcWrappingStyle).Returns(ProcWrappingStyle.Exec);
            result = container.WrapForStoredProc(ExecutionType.Write);

            // Assert
            Assert.StartsWith("EXEC", result);
        }

        [Fact]
        public void Cleanup_ClearsCommandParameters()
        {
            // Arrange
            var container = new SqlContainer(_mockContext.Object);
            container.AppendParameter("param1", DbType.String, "value");

            // Act
            container.Cleanup(null, null, ExecutionType.Read);

            // Assert
            Assert.Empty(container.Query.ToString());
        }

        [Fact]
        public void AppendParameters_AddsListToParameters()
        {
            // Arrange
            var container = new SqlContainer(_mockContext.Object);
            var parameters = new List<DbParameter>
            {
                new Mock<DbParameter>().Object,
                new Mock<DbParameter>().Object
            };

            // Act
            container.AppendParameters(parameters);

            // Assert
            Assert.Equal(2, container.Query.Length); // Check if two parameters have been added.
        }
    }
