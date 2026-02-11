namespace Application.Exceptions;

public class AppException : Exception
{
    protected AppException(string message) : base(message) { }
}

public class NotFoundException(string message) : AppException(message);

public class BadRequestException(string message) : AppException(message);