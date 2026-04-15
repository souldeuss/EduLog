using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduLog.Migrations
{
    /// <inheritdoc />
    public partial class FixSubjectTeacherDeleteBehavior : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SubjectTeacher_Teacher_TeacherId",
                table: "SubjectTeacher");

            migrationBuilder.AddForeignKey(
                name: "FK_SubjectTeacher_Teacher_TeacherId",
                table: "SubjectTeacher",
                column: "TeacherId",
                principalTable: "Teacher",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SubjectTeacher_Teacher_TeacherId",
                table: "SubjectTeacher");

            migrationBuilder.AddForeignKey(
                name: "FK_SubjectTeacher_Teacher_TeacherId",
                table: "SubjectTeacher",
                column: "TeacherId",
                principalTable: "Teacher",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
