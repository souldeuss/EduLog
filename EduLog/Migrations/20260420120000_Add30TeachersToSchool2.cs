using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EduLog.Migrations
{
    /// <inheritdoc />
    public partial class Add30TeachersToSchool2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Teacher",
                columns: new[] { "Id", "Name", "Surname", "Patronymic", "SchoolId", "PhotoPath" },
                values: new object[,]
                {
                    { 6, "Ірина", "Сідак", "Сергіївна", 2, "" },
                    { 7, "Петро", "Коваленко", "Іванович", 2, "" },
                    { 8, "Ольга", "Морозова", "Миколаївна", 2, "" },
                    { 9, "Андрій", "Цимбалюк", "Сергійович", 2, "" },
                    { 10, "Наталія", "Савенко", "Олегівна", 2, "" },
                    { 11, "Валерій", "Грінь", "Вікторович", 2, "" },
                    { 12, "Світлана", "Гаврилець", "Миколаївна", 2, "" },
                    { 13, "Геннадій", "Панченко", "Яковлевич", 2, "" },
                    { 14, "Галина", "Чорна", "Миколаївна", 2, "" },
                    { 15, "Юрій", "Гусак", "Петрович", 2, "" },
                    { 16, "Лариса", "Малова", "Ігорівна", 2, "" },
                    { 17, "Сергій", "Зінкевич", "Вікторович", 2, "" },
                    { 18, "Валентина", "Кравченко", "Павлівна", 2, "" },
                    { 19, "Олег", "Шевченко", "Ігорич", 2, "" },
                    { 20, "Людмила", "Петренко", "Йосипівна", 2, "" },
                    { 21, "Игорь", "Лысенко", "Сергеевич", 2, "" },
                    { 22, "Ельвира", "Никитина", "Викторовна", 2, "" },
                    { 23, "Станислав", "Владимиров", "Петрович", 2, "" },
                    { 24, "Наталья", "Орлова", "Ивановна", 2, "" },
                    { 25, "Павел", "Соколов", "Константинович", 2, "" },
                    { 26, "Берта", "Полякова", "Алексеевна", 2, "" },
                    { 27, "Максим", "Семенов", "Геннадиевич", 2, "" },
                    { 28, "Надежда", "Волова", "Сергеевна", 2, "" },
                    { 29, "Артур", "Завальей", "Владимирович", 2, "" },
                    { 30, "Марина", "Кулик", "Ивановна", 2, "" },
                    { 31, "Владимир", "Горобец", "Юрьевич", 2, "" },
                    { 32, "Елена", "Гущина", "Николаевна", 2, "" },
                    { 33, "Евгений", "Кулаков", "Сергеевич", 2, "" },
                    { 34, "Виктория", "Сидоренко", "Иосифовна", 2, "" },
                    { 35, "Виктор", "Новиков", "Федорович", 2, "" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Teacher",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Teacher",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Teacher",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "Teacher",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "Teacher",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "Teacher",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "Teacher",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "Teacher",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "Teacher",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "Teacher",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "Teacher",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "Teacher",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "Teacher",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "Teacher",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "Teacher",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "Teacher",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "Teacher",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "Teacher",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "Teacher",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "Teacher",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "Teacher",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "Teacher",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "Teacher",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "Teacher",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "Teacher",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "Teacher",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "Teacher",
                keyColumn: "Id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "Teacher",
                keyColumn: "Id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "Teacher",
                keyColumn: "Id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "Teacher",
                keyColumn: "Id",
                keyValue: 35);
        }
    }
}
