using System.Data;

using Dapper;

using DapperToolkit.Core.Context;
using DapperToolkit.Core.Interfaces;

using Moq;

namespace DapperToolkit.CoreTests;

public class DapperDbContextTests
{
    [Fact]
    public async Task Should_Open_Connection_When_Closed()
    {
        // Arrange
        var mockConnection = new Mock<IDbConnection>();
        mockConnection.Setup(c => c.State).Returns(ConnectionState.Closed);
        mockConnection.Setup(c => c.Open());

        var mockProvider = new Mock<IDapperConnectionProvider>();
        mockProvider.Setup(p => p.CreateConnection()).Returns(mockConnection.Object);

        var context = new DapperDbContext(mockProvider.Object);

        // Act
        try
        {
            await context.ExecuteAsync("SELECT 1"); // Bu sətir Dapper-ə girəcək və exception ata bilər
        }
        catch
        {
            // Nəticəni yoxlamırıq, sadəcə Open çağırılıb ya yox onu yoxlayırıq
        }

        // Assert
        mockConnection.Verify(c => c.Open(), Times.Once);
    }
}