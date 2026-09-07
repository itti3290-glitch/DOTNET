namespace LibaryCore.DTOs.Responses;

/// <summary>Public view of a user - never carries the password hash.</summary>
public record UserResponse
{
    public int Id { get; init; }
    public string UserName { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string FullName { get; init; } = default!;
    public string Role { get; init; } = default!;
}

/// <summary>Returned by register and login. The bearer token goes in the <c>Authorization</c> header.</summary>
public record AuthResponse
{
    public string AccessToken { get; init; } = default!;
    public DateTime ExpiresAtUtc { get; init; }
    public UserResponse User { get; init; } = default!;
}

/// <summary>Public view of a catalog book.</summary>
public record BookResponse
{
    public int Id { get; init; }
    public string Title { get; init; } = default!;
    public string Author { get; init; } = default!;
    public string Isbn { get; init; } = default!;
    public int PublicationYear { get; init; }
    public string? Description { get; init; }
    public string Status { get; init; } = default!;
    public DateTime CreatedAtUtc { get; init; }
}

/// <summary>Public view of one borrow event.</summary>
public record LoanResponse
{
    public int Id { get; init; }
    public int BookId { get; init; }
    public string BookTitle { get; init; } = default!;
    public int MemberId { get; init; }
    public string MemberName { get; init; } = default!;
    public DateTime BorrowedAtUtc { get; init; }
    public DateTime DueAtUtc { get; init; }
    public DateTime? ReturnedAtUtc { get; init; }
    public string Status { get; init; } = default!;

    /// <summary>True when the loan is still active and <see cref="DueAtUtc"/> is in the past.</summary>
    public bool IsOverdue { get; init; }
}

/// <summary>One page of a larger result set.</summary>
public record PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public static PagedResult<T> Create(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
        => new() { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
}
