namespace Nexus.Hub.Domain.Entities;

public class User
{
    public string Id { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
