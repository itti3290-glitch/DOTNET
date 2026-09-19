using AutoMapper;
using Library.Core.Dtos;
using Library.Core.Entities;
using Library.Core.Exceptions;
using Library.Core.Interfaces;

namespace Library.Service.Services;

public class BookService(
    IBookRepository books,
    ICategoryRepository categories,
    ILoanRepository loans,
    IUnitOfWork uow,
    IMapper mapper) : IBookService
{
    public async Task<PagedResult<BookResponse>> GetAllAsync(PageQuery query, CancellationToken ct)
    {
        var (items, total) = await books.GetPagedAsync(query.SafePage, query.SafePageSize, ct);
        return new PagedResult<BookResponse>(mapper.Map<List<BookResponse>>(items), query.SafePage, query.SafePageSize, total);
    }

    public async Task<BookResponse> GetByIdAsync(int id, CancellationToken ct)
    {
        var book = await books.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Book), id);
        return mapper.Map<BookResponse>(book);
    }

    public async Task<BookResponse> CreateAsync(CreateBookRequest request, CancellationToken ct)
    {
        Validate(request.Title, request.Author, request.Isbn, request.TotalCopies);
        if (await books.IsbnExistsAsync(request.Isbn, null, ct))
            throw new BusinessRuleException($"A book with ISBN '{request.Isbn}' already exists.");

        var book = mapper.Map<Book>(request);
        book.AvailableCopies = book.TotalCopies;
        await SetCategoriesAsync(book, request.CategoryIds, ct);

        await books.AddAsync(book, ct);
        await uow.SaveChangesAsync(ct);
        return mapper.Map<BookResponse>(book);
    }

    public async Task UpdateAsync(int id, UpdateBookRequest request, CancellationToken ct)
    {
        Validate(request.Title, request.Author, request.Isbn, request.TotalCopies);
        var book = await books.GetForUpdateAsync(id, ct) ?? throw new NotFoundException(nameof(Book), id);

        if (await books.IsbnExistsAsync(request.Isbn, id, ct))
            throw new BusinessRuleException($"A book with ISBN '{request.Isbn}' already exists.");

        // Copies currently on loan stay on loan; the new total can't drop below them.
        var onLoan = book.TotalCopies - book.AvailableCopies;
        if (request.TotalCopies < onLoan)
            throw new BusinessRuleException($"TotalCopies cannot be less than the {onLoan} copies currently on loan.");

        mapper.Map(request, book);
        book.TotalCopies = request.TotalCopies;
        book.AvailableCopies = request.TotalCopies - onLoan;
        await SetCategoriesAsync(book, request.CategoryIds, ct);

        await uow.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var book = await books.GetForUpdateAsync(id, ct) ?? throw new NotFoundException(nameof(Book), id);
        if (await loans.BookHasLoansAsync(id, ct))
            throw new BusinessRuleException("A book with loan history cannot be deleted.");

        books.Remove(book);
        await uow.SaveChangesAsync(ct);
    }

    private async Task SetCategoriesAsync(Book book, IReadOnlyList<int>? ids, CancellationToken ct)
    {
        book.Categories.Clear();
        if (ids is not { Count: > 0 }) return;

        var distinct = ids.Distinct().ToList();
        var found = await categories.GetByIdsAsync(distinct, ct);
        var missing = distinct.Except(found.Select(c => c.Id)).ToList();
        if (missing.Count > 0)
            throw new ValidationException($"Unknown category id(s): {string.Join(", ", missing)}.");

        foreach (var category in found) book.Categories.Add(category);
    }

    private static void Validate(string title, string author, string isbn, int totalCopies)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ValidationException("Title is required.");
        if (string.IsNullOrWhiteSpace(author)) throw new ValidationException("Author is required.");
        if (string.IsNullOrWhiteSpace(isbn)) throw new ValidationException("ISBN is required.");
        if (totalCopies < 0) throw new ValidationException("TotalCopies cannot be negative.");
    }
}
