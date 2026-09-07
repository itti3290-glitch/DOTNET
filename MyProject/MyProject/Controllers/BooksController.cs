using LibaryCore.DTOs.Requests;
using LibaryCore.DTOs.Responses;
using LibaryCore.Enums;
using LibaryCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Libary.Api.Controllers;

[ApiController]
[Route("api/books")]
[Authorize]
public sealed class BooksController : ControllerBase
{
    private readonly IBookService _bookService;

    public BooksController(IBookService bookService) => _bookService = bookService;

    /// <summary>Browse the catalog. Any authenticated user.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<BookResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<BookResponse>>> GetBooks(
        [FromQuery] BookQuery query, CancellationToken ct)
        => Ok(await _bookService.GetBooksAsync(query, ct));

    /// <summary>A single book by id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(BookResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookResponse>> GetBook(int id, CancellationToken ct)
        => Ok(await _bookService.GetBookAsync(id, ct));

    /// <summary>Add a book to the catalog. Manager only.</summary>
    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Manager))]
    [ProducesResponseType(typeof(BookResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookResponse>> CreateBook(CreateBookRequest request, CancellationToken ct)
    {
        var created = await _bookService.CreateBookAsync(request, ct);
        return CreatedAtAction(nameof(GetBook), new { id = created.Id }, created);
    }

    /// <summary>Update a book. Manager only.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = nameof(UserRole.Manager))]
    [ProducesResponseType(typeof(BookResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookResponse>> UpdateBook(int id, UpdateBookRequest request, CancellationToken ct)
        => Ok(await _bookService.UpdateBookAsync(id, request, ct));

    /// <summary>Delete a book that has never been borrowed. Manager only.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = nameof(UserRole.Manager))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteBook(int id, CancellationToken ct)
    {
        await _bookService.DeleteBookAsync(id, ct);
        return NoContent();
    }
}
