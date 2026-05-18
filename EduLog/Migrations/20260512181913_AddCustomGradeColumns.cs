using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduLog.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomGradeColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CustomGradeColumnId",
                table: "Grade",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomGradeColumn",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ClassId = table.Column<int>(type: "int", nullable: false),
                    SubjectId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByTeacherId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomGradeColumn", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Grade_CustomGradeColumnId",
                table: "Grade",
                column: "CustomGradeColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomGradeColumn_ClassId_SubjectId",
                table: "CustomGradeColumn",
                columns: new[] { "ClassId", "SubjectId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Grade_CustomGradeColumn_CustomGradeColumnId",
                table: "Grade",
                column: "CustomGradeColumnId",
                principalTable: "CustomGradeColumn",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Grade_CustomGradeColumn_CustomGradeColumnId",
                table: "Grade");

            migrationBuilder.DropTable(
                name: "CustomGradeColumn");

            migrationBuilder.DropIndex(
                name: "IX_Grade_CustomGradeColumnId",
                table: "Grade");

            migrationBuilder.DropColumn(
                name: "CustomGradeColumnId",
                table: "Grade");
        }
    }
}
