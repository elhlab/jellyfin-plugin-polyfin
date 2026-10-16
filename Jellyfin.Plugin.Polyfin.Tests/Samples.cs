using Jellyfin.Plugin.Polyfin.Database.Metadata;
using Jellyfin.Plugin.Polyfin.Models;

namespace Jellyfin.Plugin.Polyfin.Tests;

/// <summary>
/// Shared sample records for tests. You can vary them using <c>with</c>.
/// </summary>
/// <remarks>
/// Pick the sample that matches what the test means, so it stays true when fields are added:
/// <list type="bullet">
/// <item>Any fields, or every field: use a <c>Full</c> sample.</item>
/// <item>Only some fields: start from an <c>Empty</c> sample and set them.</item>
/// <item>Every field except some: start from a <c>Full</c> sample and set those to <see langword="null"/>.</item>
/// </list>
/// <para>
/// The samples describe one item, so converting a sample gives the matching sample of the other type.
/// They all hold the same <see cref="Name"/>, <see cref="Overview"/> and <see cref="Tagline"/>. The
/// <see cref="Metadata"/> and <see cref="StoredMetadata"/> samples also share <see cref="ItemId"/>, and the
/// stored samples also share the <see cref="German"/> locale and <see cref="RefreshedAt"/>.
/// </para>
/// <para>
/// Storing two stored samples writes the same row. Give each its own <c>Guid</c> when a test needs
/// separate rows.
/// </para>
/// </remarks>
internal static class Samples
{
    /// <summary>
    /// The item id of the plain and stored samples.
    /// </summary>
    public static readonly Guid ItemId = Guid.NewGuid();

    /// <summary>
    /// The locale of the stored samples.
    /// </summary>
    public static readonly Locale German = new Locale("de", "DE");

    /// <summary>
    /// A second locale, for tests that need one different from <see cref="German"/>.
    /// </summary>
    public static readonly Locale French = new Locale("fr", "FR");

    /// <summary>
    /// The name used in every <c>Full</c> sample.
    /// </summary>
    public const string Name = "Der Titel";

    /// <summary>
    /// The overview used in every <c>Full</c> sample.
    /// </summary>
    public const string Overview = "Die Beschreibung";

    /// <summary>
    /// The tagline used in every <c>Full</c> sample.
    /// </summary>
    public const string Tagline = "Der Slogan";

    /// <summary>
    /// The refresh time of the stored samples.
    /// </summary>
    public static readonly DateTimeOffset RefreshedAt = new DateTimeOffset(2022, 6, 30, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Every field set. Use it when the test:
    /// <list type="bullet">
    /// <item>needs every field,</item>
    /// <item>doesn't care which fields are set, or</item>
    /// <item>needs all but some (set those to <see langword="null"/>).</item>
    /// </list>
    /// </summary>
    public static readonly FetchedMetadata FullFetched = new FetchedMetadata(Name, Overview, Tagline);

    /// <summary>
    /// No fields set. Use it when the test needs only some fields, and set those using <c>with</c>.
    /// </summary>
    public static readonly FetchedMetadata EmptyFetched = new FetchedMetadata(null, null, null);

    /// <summary>
    /// Every field set. Use it when the test:
    /// <list type="bullet">
    /// <item>needs every field,</item>
    /// <item>doesn't care which fields are set, or</item>
    /// <item>needs all but some (set those to <see langword="null"/>).</item>
    /// </list>
    /// </summary>
    public static readonly Metadata FullMetadata = new Metadata(ItemId, Name, Overview, Tagline);

    /// <summary>
    /// No fields set. Use it when the test needs only some fields, and set those using <c>with</c>.
    /// </summary>
    public static readonly Metadata EmptyMetadata = new Metadata(ItemId, null, null, null);

    /// <summary>
    /// Every field set. Use it when the test:
    /// <list type="bullet">
    /// <item>needs every field,</item>
    /// <item>doesn't care which fields are set, or</item>
    /// <item>needs all but some (set those to <see langword="null"/>).</item>
    /// </list>
    /// Same row as <see cref="EmptyStored"/> in the store.
    /// </summary>
    public static readonly StoredMetadata FullStored = new StoredMetadata(ItemId, German, Name, Overview, Tagline, RefreshedAt);

    /// <summary>
    /// No fields set. Use it when the test needs only some fields, and set those using <c>with</c>.
    /// Same row as <see cref="FullStored"/> in the store.
    /// </summary>
    public static readonly StoredMetadata EmptyStored = new StoredMetadata(ItemId, German, null, null, null, RefreshedAt);
}
