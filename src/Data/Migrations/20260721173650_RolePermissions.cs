using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoList.Data.Migrations
{
    /// <inheritdoc />
    public partial class RolePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "permissions",
                table: "project_access_roles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill so no existing member loses access when the coarse model becomes granular.
            // Integer values are ProjectPermission flags:
            //   DefaultMember (AllTodos|AllNotes|ReferenceRead)              = 511
            //   DefaultMember | ManageMembers                                = 246271
            // Existing project_access_roles rows previously meant "this role can manage access", so
            // they get the default member grant plus the member-management bits.
            migrationBuilder.Sql(
                "UPDATE project_access_roles SET permissions = 246271 WHERE permissions = 0;");

            // Every (project, role) currently assigned to an accepted member (status = 1) that has no
            // grant row gets the default member permission set, preserving prior view+edit behavior.
            migrationBuilder.Sql(@"
                INSERT INTO project_access_roles (id, project_id, role_id, permissions)
                SELECT gen_random_uuid(), m.project_id, m.role_id, 511
                FROM project_members m
                WHERE m.status = 1
                  AND NOT EXISTS (
                      SELECT 1 FROM project_access_roles r
                      WHERE r.project_id = m.project_id AND r.role_id = m.role_id)
                GROUP BY m.project_id, m.role_id;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "permissions",
                table: "project_access_roles");
        }
    }
}
