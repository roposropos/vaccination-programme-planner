namespace VaxPlan.Api.Domain;

public class DomainException(string message) : Exception(message);

public class NotFoundException(string message) : Exception(message);
