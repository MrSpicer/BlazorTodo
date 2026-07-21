using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoList.Data.Migrations
{
    /// <inheritdoc />
    public partial class SharingAndAccessControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "assignee_id",
                table: "todos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "owner_id",
                table: "todos",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Backfill: existing todos are solo, so their owner is the current partition user.
            migrationBuilder.Sql("UPDATE todos SET owner_id = user_id WHERE owner_id = '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.CreateTable(
                name: "project_access_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_access_roles", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_access_roles_AspNetRoles_role_id",
                        column: x => x.role_id,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_access_roles_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    invite_token = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    invited_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    can_manage_access = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_members", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_members_AspNetRoles_role_id",
                        column: x => x.role_id,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_members_AspNetUsers_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_members_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_todos_assignee_id",
                table: "todos",
                column: "assignee_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_access_roles_role_id",
                table: "project_access_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ux_project_access_roles_project_id_role_id",
                table: "project_access_roles",
                columns: new[] { "project_id", "role_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_project_members_invite_token",
                table: "project_members",
                column: "invite_token");

            migrationBuilder.CreateIndex(
                name: "IX_project_members_role_id",
                table: "project_members",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_members_user_id_status",
                table: "project_members",
                columns: new[] { "user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_project_members_project_id_user_id",
                table: "project_members",
                columns: new[] { "project_id", "user_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_access_roles");

            migrationBuilder.DropTable(
                name: "project_members");

            migrationBuilder.DropIndex(
                name: "ix_todos_assignee_id",
                table: "todos");

            migrationBuilder.DropColumn(
                name: "assignee_id",
                table: "todos");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "todos");
        }
    }
}
