using System;
using System.ComponentModel.DataAnnotations;

namespace TodoList.Models;

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required(ErrorMessage = "Project name is required")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Project name must be 1-50 characters")]
    public string Name { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "Description cannot exceed 200 characters")]
    public string Description { get; set; } = string.Empty;

    public string Color { get; set; } = "#6c757d";

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public bool IsDefault { get; set; }

    public bool IsValid()
    {
        return Id != Guid.Empty && !string.IsNullOrWhiteSpace(Name);
    }
}
