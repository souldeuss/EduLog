using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduLog.Migrations
{
    /// <inheritdoc />
    public partial class ExtendInvitationForStudents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Invitation",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Teacher");

            migrationBuilder.AddColumn<int>(
                name: "StudentId",
                table: "Invitation",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invitation_StudentId",
                table: "Invitation",
                column: "StudentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Invitation_Student_StudentId",
                table: "Invitation",
                column: "StudentId",
                principalTable: "Student",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invitation_Student_StudentId",
                table: "Invitation");

            migrationBuilder.DropIndex(
                name: "IX_Invitation_StudentId",
                table: "Invitation");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Invitation");

            migrationBuilder.DropColumn(
                name: "StudentId",
                table: "Invitation");
        }
    }
}
