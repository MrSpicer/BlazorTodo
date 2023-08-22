using System;

public class TodoItem
{
	public string Title {get; set;} = string.Empty;
	public bool IsDone { get; set; }

	public bool IsValid(){
		return !String.IsNullOrWhiteSpace(Title);
	}
}