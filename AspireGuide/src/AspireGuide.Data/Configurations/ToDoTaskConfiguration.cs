using AspireGuide.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AspireGuide.Data.Configurations;

public class ToDoTaskConfiguration : IEntityTypeConfiguration<ToDoTask>
{
    public void Configure(EntityTypeBuilder<ToDoTask> builder)
    {
        builder.ToTable("Tasks");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .UseIdentityColumn();

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(t => t.Description)
            .HasColumnType("text");

        builder.Property(t => t.IsCompleted)
            .HasDefaultValue(false);

        builder.Property(t => t.DueDate)
            .HasColumnType("date");

        builder.Property(t => t.CreatedAt)
            .HasDefaultValueSql("GETDATE()");

        builder.Property(t => t.TodoId)
            .IsRequired();

        builder.HasOne(t => t.Todo)
            .WithMany(todo => todo.Tasks)
            .HasForeignKey(t => t.TodoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
