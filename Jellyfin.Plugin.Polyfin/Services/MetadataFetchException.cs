using System;

namespace Jellyfin.Plugin.Polyfin.Services;

/// <summary>
/// Thrown when metadata could not be fetched: the item was not found, or the providers failed.
/// Not thrown when the providers answered but had nothing in the requested language.
/// </summary>
public class MetadataFetchException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataFetchException"/> class.
    /// </summary>
    public MetadataFetchException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataFetchException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public MetadataFetchException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataFetchException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The provider errors that caused this one.</param>
    public MetadataFetchException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
