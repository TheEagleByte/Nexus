namespace Nexus.Hub.Domain.Exceptions;

public class DomainException : Exception
{
    public string Code { get; }

    public DomainException(string message, string code = "BAD_REQUEST")
        : base(message)
    {
        Code = code;
    }
}

public class NotFoundException : DomainException
{
    public NotFoundException(string message, string code = "NOT_FOUND")
        : base(message, code) { }
}

public class ConflictException : DomainException
{
    public ConflictException(string message, string code = "CONFLICT")
        : base(message, code) { }
}

public class ValidationException : DomainException
{
    public ValidationException(string message, string code = "VALIDATION_ERROR")
        : base(message, code) { }
}
