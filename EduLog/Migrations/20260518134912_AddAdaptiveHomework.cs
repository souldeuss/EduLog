using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduLog.Migrations
{
    /// <inheritdoc />
    public partial class AddAdaptiveHomework : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuestionItem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubjectId = table.Column<int>(type: "int", nullable: false),
                    TopicTag = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IrtA = table.Column<double>(type: "float", nullable: false),
                    IrtB = table.Column<double>(type: "float", nullable: false),
                    IrtC = table.Column<double>(type: "float", nullable: false),
                    HintText = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionItem_Subject_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subject",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentKnowledgeState",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    SubjectId = table.Column<int>(type: "int", nullable: false),
                    TopicTag = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProbabilityLearned = table.Column<double>(type: "float", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentKnowledgeState", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentKnowledgeState_Student_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Student",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentKnowledgeState_Subject_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subject",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdaptiveSession",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    LessonMaterialId = table.Column<int>(type: "int", nullable: false),
                    CurrentQuestionId = table.Column<int>(type: "int", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdaptiveSession", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdaptiveSession_LessonMaterial_LessonMaterialId",
                        column: x => x.LessonMaterialId,
                        principalTable: "LessonMaterial",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AdaptiveSession_QuestionItem_CurrentQuestionId",
                        column: x => x.CurrentQuestionId,
                        principalTable: "QuestionItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AdaptiveSession_Student_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Student",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdaptiveAnswer",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    QuestionId = table.Column<int>(type: "int", nullable: false),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: false),
                    AnsweredAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdaptiveAnswer", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdaptiveAnswer_AdaptiveSession_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AdaptiveSession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AdaptiveAnswer_QuestionItem_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "QuestionItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdaptiveAnswer_QuestionId",
                table: "AdaptiveAnswer",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_AdaptiveAnswer_SessionId",
                table: "AdaptiveAnswer",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AdaptiveSession_CurrentQuestionId",
                table: "AdaptiveSession",
                column: "CurrentQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_AdaptiveSession_LessonMaterialId",
                table: "AdaptiveSession",
                column: "LessonMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_AdaptiveSession_StudentId",
                table: "AdaptiveSession",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionItem_SubjectId_TopicTag",
                table: "QuestionItem",
                columns: new[] { "SubjectId", "TopicTag" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentKnowledgeState_StudentId_SubjectId_TopicTag",
                table: "StudentKnowledgeState",
                columns: new[] { "StudentId", "SubjectId", "TopicTag" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentKnowledgeState_SubjectId",
                table: "StudentKnowledgeState",
                column: "SubjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdaptiveAnswer");

            migrationBuilder.DropTable(
                name: "StudentKnowledgeState");

            migrationBuilder.DropTable(
                name: "AdaptiveSession");

            migrationBuilder.DropTable(
                name: "QuestionItem");
        }
    }
}
