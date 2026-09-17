using Microsoft.AspNetCore.Http;
using NpAspire.Api.Common;

namespace NpAspire.Api.Tests.Common;

public class ServerResultTests
{
    [Fact]
    public void NewResult_HasNoMessages_AndIsSuccessful()
    {
        var result = new ServerResult<string>();

        Assert.Null(result.Data);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.False(result.HasErrors());
        Assert.False(result.HasWarnings());
        Assert.False(result.HasSuccesses());
        Assert.True(result.IsSuccessful());
    }

    [Fact]
    public void Data_KeepsTheGivenType()
    {
        var result = new ServerResult<int[]> { Data = [1, 2, 3] };

        Assert.Equal([1, 2, 3], result.Data);
    }

    [Fact]
    public void AddError_AppendsTheExceptionMessage()
    {
        var result = new ServerResult<string>();

        result.AddError(new InvalidOperationException("first"));
        result.AddError(new ArgumentException("second"));

        Assert.True(result.HasErrors());
        Assert.Equal(["first", "second"], result.ErrorMessages);
    }

    [Fact]
    public void AddError_AppendsTheStackTrace_WhenRequested()
    {
        var result = new ServerResult<string>();
        var exception = Assert.Throws<InvalidOperationException>(Fail);

        result.AddError(exception, includeStackTrace: true);
        result.AddError(exception, includeStackTrace: true);

        Assert.Equal([exception.StackTrace!, exception.StackTrace!], result.StackTrace);
    }

    [Fact]
    public void AddError_OmitsTheStackTrace_ByDefault()
    {
        var result = new ServerResult<string>();
        var exception = Assert.Throws<InvalidOperationException>(Fail);

        result.AddError(exception);

        Assert.Equal(["thrown"], result.ErrorMessages);
        Assert.Empty(result.StackTrace);
    }

    [Fact]
    public void AddError_SkipsTheStackTrace_OfAnExceptionThatWasNeverThrown()
    {
        var result = new ServerResult<string>();

        result.AddError(new InvalidOperationException("not thrown"), includeStackTrace: true);

        Assert.Single(result.ErrorMessages);
        Assert.Empty(result.StackTrace);
    }

    [Fact]
    public void AddError_Throws_WhenExceptionIsNull()
    {
        var result = new ServerResult<string>();

        Assert.Throws<ArgumentNullException>(() => result.AddError(null!));
    }

    [Fact]
    public void HasWarnings_IsTrue_WhenAWarningIsAdded()
    {
        var result = new ServerResult<string>();

        result.WarningMessages.Add("careful");

        Assert.True(result.HasWarnings());
    }

    [Fact]
    public void HasSuccesses_IsTrue_WhenASuccessIsAdded()
    {
        var result = new ServerResult<string>();

        result.SuccessMessages.Add("saved");

        Assert.True(result.HasSuccesses());
    }

    [Theory]
    [InlineData(199, false)]
    [InlineData(200, true)]
    [InlineData(204, true)]
    [InlineData(299, true)]
    [InlineData(300, false)]
    [InlineData(404, false)]
    [InlineData(500, false)]
    public void IsSuccessful_IsTrue_OnlyFor2xx(int statusCode, bool expected)
    {
        var result = new ServerResult<string> { StatusCode = statusCode };

        Assert.Equal(expected, result.IsSuccessful());
    }

    private static void Fail() => throw new InvalidOperationException("thrown");
}
