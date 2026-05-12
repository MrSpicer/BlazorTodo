namespace TodoList.Models;

public interface IProjectScoped
{
	Guid ProjectId { get; set; }
}
