using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoList.Data.Migrations
{
    /// <inheritdoc />
    public partial class WidenTodoDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "todos",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "todos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);
        }
    }
}
