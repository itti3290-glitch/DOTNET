using LibaryCore.DTOs.Requests;
using LibaryCore.DTOs.Responses;
using LibaryCore.Entities;
using LibaryCore.Enums;
using LibaryCore.Exceptions;
using LibaryCore.Services;
using LibaryData.Repositories;

namespace LibaryService.Services;

/// <inheritdoc cref="IBookService"/>
public sealed class BookService : IBookService
{
    private readonly IBookRepository _books;
    private readonly IUnitOfWork _unitOfWork;

    public BookService(IBookRepository books, IUnitOfWork unitOfWork)
    {
        _books = books;
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<BookResponse>> GetBooksAsync(BookQuery query, CancellationToken ct = default)
    {
        var (items, total) = await _books.SearchAsync(
            query.Search, query.Status, query.Page, query.PageSize, ct);

        var mapped = items.Select(b => b.ToResponse()).ToList();
        return PagedResult<BookResponse>.Create(mapped, query.Page, query.PageSize, total);
    }

    public async Task<BookResponse> GetBookAsync(int id, CancellationToken ct = default)
    {
        var book = await _books.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Book", id);

        return book.ToResponse();
    }

    public async Task<BookResponse> CreateBookAsync(CreateBookRequest request, CancellationToken ct = default)
    {
        var isbn = request.Isbn.Trim();

        if (await _books.IsbnExistsAsync(isbn, excludingBookId: null, ct))
            throw new ConflictException($"A book with ISBN '{isbn}' already exists.");

        var book = new Book
        {
            Title = request.Title.Trim(),
            Author = request.Author.Trim(),
            Isbn = isbn,
            PublicationYear = request.PublicationYear,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Status = BookStatus.Available,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _books.AddAsync(book, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return book.ToResponse();
    }

    public async Task<BookResponse> UpdateBookAsync(int id, UpdateBookRequest request, CancellationToken ct = default)
    {
        var book = await _books.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Book", id);

        var isbn = request.Isbn.Trim();
        if (await _books.IsbnExistsAsync(isbn, excludingBookId: id, ct))
            throw new ConflictException($"Another book already uses ISBN '{isbn}'.");

        if (request.Status is not (BookStatus.Available or BookStatus.Maintenance))
            throw new ValidationException(
                $"Status can only be set to '{BookStatus.Available}' or '{BookStatus.Maintenance}'.");

        if (book.Status == BookStatus.Loaned && request.Status != BookStatus.Loaned)
            throw new ConflictException("This book is on loan and cannot change status until it is returned.");

        book.Title = request.Title.Trim();
        book.Author = request.Author.Trim();
        book.Isbn = isbn;
        book.PublicationYear = request.PublicationYear;
        book.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        book.Status = request.Status;

        await _unitOfWork.SaveChangesAsync(ct);

        return book.ToResponse();
    }

    public async Task DeleteBookAsync(int id, CancellationToken ct = default)
    {
        var book = await _books.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Book", id);

        if (book.Status == BookStatus.Loaned)
            throw new ConflictException("This book is on loan and cannot be deleted.");

        // Loan FKs are Restrict, so a book with borrow history cannot be removed. Surface that as
        // a clean 409 instead of letting the database throw.
        try
        {
            _books.Remove(book);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not DomainException)
        {
            throw new ConflictException(
                "This book has loan history and cannot be deleted. Put it in maintenance instead.");
        }
    }
}
