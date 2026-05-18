using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduLog.Migrations
{
    /// <inheritdoc />
    public partial class AddLessonMaterialEduCoinReward : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EduCoinReward",
                table: "LessonMaterial",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EduCoinReward",
                table: "LessonMaterial");
        }
    }
}
