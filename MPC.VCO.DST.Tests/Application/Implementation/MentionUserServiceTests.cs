using MPC.VCO.DST.Application;
using MPC.VCO.DST.Application.Interfaces;
using MPC.VCO.DST.Application.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace MPC.VCO.DST.Tests.Application.Implementation;

[TestClass]
public sealed class MentionUserServiceTests
{
    [TestMethod]
    public async Task SearchAsync_TrimmedSearch_DelegatesWithMaxResults()
    {
        Mock<IMentionUserDirectory> directory = new();
        directory
            .Setup(x => x.SearchAsync("alice", 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new MentionUser("1", "Alice Doe", "alice@contoso.com")
            ]);

        IMentionUserService service = CreateService(directory.Object);

        IReadOnlyList<MentionUser> result = await service.SearchAsync("  alice  ");

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Alice Doe", result[0].DisplayName);
        directory.Verify(x => x.SearchAsync("alice", 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SearchAsync_SearchExceedsMaxLength_ThrowsArgumentOutOfRangeException()
    {
        Mock<IMentionUserDirectory> directory = new();
        IMentionUserService service = CreateService(directory.Object);

        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => service.SearchAsync(new string('x', 101)));

        directory.Verify(
            x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GetAuthorizedUsersAsync_DeduplicatesAndTrimsIds_DelegatesDistinctSet()
    {
        Mock<IMentionUserDirectory> directory = new();
        IReadOnlyCollection<string>? capturedIds = null;

        directory
            .Setup(x => x.GetAuthorizedUsersAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<string>, CancellationToken>((ids, _) => capturedIds = ids)
            .ReturnsAsync([]);

        IMentionUserService service = CreateService(directory.Object);

        await service.GetAuthorizedUsersAsync(new[] { " user-1 ", "USER-1", "user-2", "   " });

        Assert.IsNotNull(capturedIds);
        Assert.AreEqual(2, capturedIds.Count);
        CollectionAssert.AreEquivalent(new[] { "user-1", "user-2" }, capturedIds.ToArray());
    }

    [TestMethod]
    public async Task GetAuthorizedUsersAsync_MentionCountExceedsLimit_DelegatesToDirectory()
    {
        Mock<IMentionUserDirectory> directory = new();
        IReadOnlyCollection<string>? capturedIds = null;

        directory
            .Setup(x => x.GetAuthorizedUsersAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<string>, CancellationToken>((ids, _) => capturedIds = ids)
            .ReturnsAsync([]);

        IMentionUserService service = CreateService(directory.Object);

        string[] ids = Enumerable.Range(1, 21).Select(index => $"u-{index}").ToArray();

        await service.GetAuthorizedUsersAsync(ids);

        Assert.IsNotNull(capturedIds);
        Assert.AreEqual(21, capturedIds.Count);

        directory.Verify(
            x => x.GetAuthorizedUsersAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static IMentionUserService CreateService(IMentionUserDirectory directory)
    {
        ServiceCollection services = new();
        services.AddApplication();
        services.AddSingleton(directory);

        ServiceProvider provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IMentionUserService>();
    }

    private static async Task AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
            Assert.Fail($"Expected exception {typeof(TException).Name} was not thrown.");
        }
        catch (TException)
        {
        }
    }
}
