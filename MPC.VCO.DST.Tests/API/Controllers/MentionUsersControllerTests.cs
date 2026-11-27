using MPC.VCO.DST.Application.Interfaces;
using MPC.VCO.DST.Application.Models;
using MPC.VCO.DST.Application.DTOs.Response;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MPC.VCO.DST.WebSite.API.Controllers;

namespace MPC.VCO.DST.Tests.API.Controllers;

[TestClass]
public sealed class MentionUsersControllerTests
{
    [TestMethod]
    public async Task SearchAsync_ValidSearch_ReturnsOkWithUsers()
    {
        Mock<IMentionUserService> service = new();
        service
            .Setup(x => x.SearchAsync("alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new MentionUser("1", "Alice Doe", "alice@contoso.com")
            ]);

        MentionUsersController controller = new(service.Object);

        ActionResult<IReadOnlyList<MentionUserResponse>> result = await controller.SearchAsync("alice", CancellationToken.None);

        Assert.IsInstanceOfType<OkObjectResult>(result.Result);
        OkObjectResult ok = (OkObjectResult)result.Result!;
        var payload = ok.Value as IReadOnlyList<MentionUserResponse>;
        Assert.IsNotNull(payload);
        Assert.AreEqual(1, payload.Count);
        Assert.AreEqual("Alice Doe", payload[0].DisplayName);
    }

    [TestMethod]
    public async Task SearchAsync_InvalidSearch_ReturnsBadRequest()
    {
        Mock<IMentionUserService> service = new();
        service
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Search cannot exceed 100 characters."));

        MentionUsersController controller = new(service.Object);

        await AssertThrowsAsync<ArgumentException>(() =>
            controller.SearchAsync(new string('x', 101), CancellationToken.None));
    }

    [TestMethod]
    public async Task SearchAsync_GraphFailure_ReturnsBadGateway()
    {
        Mock<IMentionUserService> service = new();
        service
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Graph unavailable"));

        MentionUsersController controller = new(service.Object);

        await AssertThrowsAsync<HttpRequestException>(() =>
            controller.SearchAsync("alice", CancellationToken.None));
    }

    private static async Task<TException> AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
            Assert.Fail($"Expected exception {typeof(TException).Name} was not thrown.");
            throw new InvalidOperationException("Unreachable assertion path.");
        }
        catch (TException exception)
        {
            return exception;
        }
    }
}
