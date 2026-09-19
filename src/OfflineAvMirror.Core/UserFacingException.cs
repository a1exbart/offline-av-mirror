namespace OfflineAvMirror.Core;

/// <summary>
/// A failure the user can act on: a localized reason plus an optional hint.
/// </summary>
/// <remarks>
/// Anything else that escapes the core is a bug and is reported as such.
/// </remarks>
public class UserFacingException : Exception
{
    public UserFacingException()
    {
    }

    public UserFacingException(string message)
        : base(message)
    {
    }

    public UserFacingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public UserFacingException(string message, string? hint, Exception? innerException = null)
        : base(message, innerException)
    {
        Hint = hint;
    }

    /// <summary>What to do about it, when there is something to do.</summary>
    public string? Hint { get; }
}
