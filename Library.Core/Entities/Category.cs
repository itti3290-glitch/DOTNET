namespace Library.Core.Entities;

/// <summary>A genre/subject. Many-to-many with <see cref="Book"/>.</summary>
public class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ICollection<Book> Books { get; set; } = new List<Book>();
}
