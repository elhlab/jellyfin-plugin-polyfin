using System;

namespace Jellyfin.Plugin.Polyfin.Services;

/// <summary>
/// Thrown when metadata could not be fetched because the item was not found.
/// </summary>
public class FetchItemNotFoundException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FetchItemNotFoundException"/> class.
    /// </summary>
    public FetchItemNotFoundException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FetchItemNotFoundException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public FetchItemNotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FetchItemNotFoundException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public FetchItemNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
