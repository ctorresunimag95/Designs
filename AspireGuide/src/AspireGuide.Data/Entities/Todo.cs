namespace AspireGuide.Data.Entities;

public class Todo
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsCompleted { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<ToDoTask> Tasks { get; set; } = [];
}
