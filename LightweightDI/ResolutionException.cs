namespace LightweightDI;

/// <summary>
/// Thrown when the container cannot resolve a requested service type.
/// </summary>
public class ResolutionException : Exception
{
    public ResolutionException(string message) : base(message)
    {
    }

    public ResolutionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
