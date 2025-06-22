using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EduLog.Migrations
{
    /// <inheritdoc />
    public partial class MultiTenantRolesInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 37);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 38);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 39);

            migrationBuilder.DeleteData(
                table: "Student",
                keyColumn: "Id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "Subject",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Class",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Class",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Teacher",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "SchoolId",
                table: "Teacher",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SchoolId",
                table: "Subject",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SchoolId",
                table: "Student",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SchoolId",
                table: "Grade",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SchoolId",
                table: "ClassSubject",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SchoolId",
                table: "Class",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SchoolId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SchoolId",
                table: "Absence",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "School",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_School", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Invitation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsUsed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invitation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invitation_School_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "School",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_SchoolId",
                table: "AspNetUsers",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitation_SchoolId",
                table: "Invitation",
                column: "SchoolId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_School_SchoolId",
                table: "AspNetUsers",
                column: "SchoolId",
                principalTable: "School",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_School_SchoolId",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Invitation");

            migrationBuilder.DropTable(
                name: "School");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_SchoolId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SchoolId",
                table: "Teacher");

            migrationBuilder.DropColumn(
                name: "SchoolId",
                table: "Subject");

            migrationBuilder.DropColumn(
                name: "SchoolId",
                table: "Student");

            migrationBuilder.DropColumn(
                name: "SchoolId",
                table: "Grade");

            migrationBuilder.DropColumn(
                name: "SchoolId",
                table: "ClassSubject");

            migrationBuilder.DropColumn(
                name: "SchoolId",
                table: "Class");

            migrationBuilder.DropColumn(
                name: "SchoolId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SchoolId",
                table: "Absence");

            migrationBuilder.InsertData(
                table: "Class",
                columns: new[] { "Id", "Name", "TeacherId" },
                values: new object[,]
                {
                    { 1, "5-A", 1 },
                    { 2, "5-B", null }
                });

            migrationBuilder.InsertData(
                table: "Teacher",
                columns: new[] { "Id", "Name", "Patronymic", "PhotoPath", "Surname" },
                values: new object[] { 1, "Олена", "Петрівна", "~/Data/UserImages/User-avatar.svg.png", "Іваненко" });

            migrationBuilder.InsertData(
                table: "Student",
                columns: new[] { "Id", "ClassId", "Name", "Patronymic", "Surname" },
                values: new object[,]
                {
                    { 1, 1, "Ім'я1", "По-батькові1", "Прізвище1" },
                    { 2, 1, "Ім'я2", "По-батькові2", "Прізвище2" },
                    { 3, 1, "Ім'я3", "По-батькові3", "Прізвище3" },
                    { 4, 1, "Ім'я4", "По-батькові4", "Прізвище4" },
                    { 5, 1, "Ім'я5", "По-батькові5", "Прізвище5" },
                    { 6, 1, "Ім'я6", "По-батькові6", "Прізвище6" },
                    { 7, 1, "Ім'я7", "По-батькові7", "Прізвище7" },
                    { 8, 1, "Ім'я8", "По-батькові8", "Прізвище8" },
                    { 9, 1, "Ім'я9", "По-батькові9", "Прізвище9" },
                    { 10, 1, "Ім'я10", "По-батькові10", "Прізвище10" },
                    { 11, 1, "Ім'я11", "По-батькові11", "Прізвище11" },
                    { 12, 1, "Ім'я12", "По-батькові12", "Прізвище12" },
                    { 13, 1, "Ім'я13", "По-батькові13", "Прізвище13" },
                    { 14, 1, "Ім'я14", "По-батькові14", "Прізвище14" },
                    { 15, 1, "Ім'я15", "По-батькові15", "Прізвище15" },
                    { 16, 1, "Ім'я16", "По-батькові16", "Прізвище16" },
                    { 17, 1, "Ім'я17", "По-батькові17", "Прізвище17" },
                    { 18, 1, "Ім'я18", "По-батькові18", "Прізвище18" },
                    { 19, 1, "Ім'я19", "По-батькові19", "Прізвище19" },
                    { 20, 1, "Ім'я20", "По-батькові20", "Прізвище20" },
                    { 21, 2, "Ім'я21", "По-батькові21", "Прізвище21" },
                    { 22, 2, "Ім'я22", "По-батькові22", "Прізвище22" },
                    { 23, 2, "Ім'я23", "По-батькові23", "Прізвище23" },
                    { 24, 2, "Ім'я24", "По-батькові24", "Прізвище24" },
                    { 25, 2, "Ім'я25", "По-батькові25", "Прізвище25" },
                    { 26, 2, "Ім'я26", "По-батькові26", "Прізвище26" },
                    { 27, 2, "Ім'я27", "По-батькові27", "Прізвище27" },
                    { 28, 2, "Ім'я28", "По-батькові28", "Прізвище28" },
                    { 29, 2, "Ім'я29", "По-батькові29", "Прізвище29" },
                    { 30, 2, "Ім'я30", "По-батькові30", "Прізвище30" },
                    { 31, 2, "Ім'я31", "По-батькові31", "Прізвище31" },
                    { 32, 2, "Ім'я32", "По-батькові32", "Прізвище32" },
                    { 33, 2, "Ім'я33", "По-батькові33", "Прізвище33" },
                    { 34, 2, "Ім'я34", "По-батькові34", "Прізвище34" },
                    { 35, 2, "Ім'я35", "По-батькові35", "Прізвище35" },
                    { 36, 2, "Ім'я36", "По-батькові36", "Прізвище36" },
                    { 37, 2, "Ім'я37", "По-батькові37", "Прізвище37" },
                    { 38, 2, "Ім'я38", "По-батькові38", "Прізвище38" },
                    { 39, 2, "Ім'я39", "По-батькові39", "Прізвище39" },
                    { 40, 2, "Ім'я40", "По-батькові40", "Прізвище40" }
                });

            migrationBuilder.InsertData(
                table: "Subject",
                columns: new[] { "Id", "ClassId", "Name", "TeacherId" },
                values: new object[] { 1, 1, "Англійська мова", 1 });
        }
    }
}
