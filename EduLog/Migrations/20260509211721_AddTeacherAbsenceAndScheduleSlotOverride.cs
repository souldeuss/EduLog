using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduLog.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherAbsenceAndScheduleSlotOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeacherAbsence",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    TeacherId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherAbsence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherAbsence_Teacher_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teacher",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleSlotOverride",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ScheduleSlotId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubstituteTeacherId = table.Column<int>(type: "int", nullable: true),
                    AbsenceId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleSlotOverride", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleSlotOverride_ScheduleSlot_ScheduleSlotId",
                        column: x => x.ScheduleSlotId,
                        principalTable: "ScheduleSlot",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduleSlotOverride_TeacherAbsence_AbsenceId",
                        column: x => x.AbsenceId,
                        principalTable: "TeacherAbsence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ScheduleSlotOverride_Teacher_SubstituteTeacherId",
                        column: x => x.SubstituteTeacherId,
                        principalTable: "Teacher",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleSlotOverride_AbsenceId",
                table: "ScheduleSlotOverride",
                column: "AbsenceId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleSlotOverride_ScheduleSlotId_Date",
                table: "ScheduleSlotOverride",
                columns: new[] { "ScheduleSlotId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleSlotOverride_SubstituteTeacherId",
                table: "ScheduleSlotOverride",
                column: "SubstituteTeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAbsence_TeacherId",
                table: "TeacherAbsence",
                column: "TeacherId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduleSlotOverride");

            migrationBuilder.DropTable(
                name: "TeacherAbsence");
        }
    }
}
