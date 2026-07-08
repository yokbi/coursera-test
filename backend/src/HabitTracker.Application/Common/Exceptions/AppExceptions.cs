namespace HabitTracker.Application.Common.Exceptions;

/// <summary>Maps to 404. Message is safe to show to clients.</summary>
public class NotFoundException(string message) : Exception(message);

/// <summary>Maps to 401. Message is intentionally generic to prevent enumeration.</summary>
public class UnauthorizedAppException(string message) : Exception(message);

/// <summary>Maps to 409.</summary>
public class ConflictException(string message) : Exception(message);

/// <summary>Maps to 422/400 for domain rule violations (e.g. check-in outside the edit window).</summary>
public class DomainRuleException(string message) : Exception(message);
