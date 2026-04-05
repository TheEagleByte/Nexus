using Nexus.Hub.Domain.Exceptions;

namespace Nexus.Hub.Api.Tests.Domain;

public class DomainExceptionTests
{
    [Fact]
    public void DomainException_DefaultCode_IsBadRequest()
    {
        var ex = new DomainException("test message");

        Assert.Equal("BAD_REQUEST", ex.Code);
        Assert.Equal("test message", ex.Message);
    }

    [Fact]
    public void DomainException_CustomCode_IsPreserved()
    {
        var ex = new DomainException("msg", "CUSTOM");

        Assert.Equal("CUSTOM", ex.Code);
    }

    [Fact]
    public void NotFoundException_DefaultCode_IsNotFound()
    {
        var ex = new NotFoundException("not found");

        Assert.Equal("NOT_FOUND", ex.Code);
        Assert.Equal("not found", ex.Message);
    }

    [Fact]
    public void NotFoundException_CustomCode_IsPreserved()
    {
        var ex = new NotFoundException("msg", "CUSTOM_NOT_FOUND");

        Assert.Equal("CUSTOM_NOT_FOUND", ex.Code);
    }

    [Fact]
    public void ConflictException_DefaultCode_IsConflict()
    {
        var ex = new ConflictException("conflict");

        Assert.Equal("CONFLICT", ex.Code);
        Assert.Equal("conflict", ex.Message);
    }

    [Fact]
    public void ConflictException_CustomCode_IsPreserved()
    {
        var ex = new ConflictException("msg", "CUSTOM_CONFLICT");

        Assert.Equal("CUSTOM_CONFLICT", ex.Code);
    }

    [Fact]
    public void ValidationException_DefaultCode_IsValidationError()
    {
        var ex = new ValidationException("invalid");

        Assert.Equal("VALIDATION_ERROR", ex.Code);
        Assert.Equal("invalid", ex.Message);
    }

    [Fact]
    public void ValidationException_CustomCode_IsPreserved()
    {
        var ex = new ValidationException("msg", "CUSTOM_VALIDATION");

        Assert.Equal("CUSTOM_VALIDATION", ex.Code);
    }

    [Fact]
    public void AllExceptions_AreSubclassOfDomainException()
    {
        Assert.IsAssignableFrom<DomainException>(new NotFoundException("test"));
        Assert.IsAssignableFrom<DomainException>(new ConflictException("test"));
        Assert.IsAssignableFrom<DomainException>(new ValidationException("test"));
    }

    [Fact]
    public void AllExceptions_AreSubclassOfSystemException()
    {
        Assert.IsAssignableFrom<Exception>(new DomainException("test"));
        Assert.IsAssignableFrom<Exception>(new NotFoundException("test"));
        Assert.IsAssignableFrom<Exception>(new ConflictException("test"));
        Assert.IsAssignableFrom<Exception>(new ValidationException("test"));
    }
}
