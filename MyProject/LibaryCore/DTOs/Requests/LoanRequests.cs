using System.ComponentModel.DataAnnotations;
using LibaryCore.Enums;

namespace LibaryCore.DTOs.Requests;

/// <summary>Payload for <c>POST /api/loans</c> - the borrow attempt.</summary>
public record BorrowBookRequest
{
    [Range(1, int.MaxValue)]
    public int BookId { get; init; }
}

/// <summary>Query-string options for the loan listing endpoints.</summary>
public record LoanQuery
{
    public LoanStatus? Status { get; init; }

    /// <summary>When true, return only active loans whose due date has passed.</summary>
    public bool OverdueOnly { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}
