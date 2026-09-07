using System.ComponentModel.DataAnnotations;
using LibaryCore.Enums;

namespace LibaryCore.DTOs.Requests;

/// <summary>Create a catalog entry for one physical book. Manager only.</summary>
public record CreateBookRequest
{
    [Required, StringLength(200)]
    public string Title { get; init; } = default!;

    [Required, StringLength(120)]
    public string Author { get; init; } = default!;

    /// <summary>10- or 13-character ISBN; digits only, must be unique across the catalog.</summary>
    [Required, RegularExpression(@"^\d{10}(\d{3})?$", ErrorMessage = "ISBN must be 10 or 13 digits.")]
    public string Isbn { get; init; } = default!;

    [Range(1450, 2100)]
    public int PublicationYear { get; init; }

    [StringLength(2000)]
    public string? Description { get; init; }
}

/// <summary>
/// Replace the editable fields of an existing book. Manager only. <see cref="Status"/> lets a
/// manager pull a book out of circulation (<see cref="BookStatus.Maintenance"/>) or put it
/// back; it cannot be used to force a book to <see cref="BookStatus.Loaned"/> - only the
/// borrow flow does that.
/// </summary>
public record UpdateBookRequest
{
    [Required, StringLength(200)]
    public string Title { get; init; } = default!;

    [Required, StringLength(120)]
    public string Author { get; init; } = default!;

    [Required, RegularExpression(@"^\d{10}(\d{3})?$", ErrorMessage = "ISBN must be 10 or 13 digits.")]
    public string Isbn { get; init; } = default!;

    [Range(1450, 2100)]
    public int PublicationYear { get; init; }

    [StringLength(2000)]
    public string? Description { get; init; }

    /// <summary>Only <see cref="BookStatus.Available"/> and <see cref="BookStatus.Maintenance"/> are accepted.</summary>
    public BookStatus Status { get; init; }
}

/// <summary>Query-string options for <c>GET /api/books</c>.</summary>
public record BookQuery
{
    /// <summary>Case-insensitive match against title or author.</summary>
    public string? Search { get; init; }

    public BookStatus? Status { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}
