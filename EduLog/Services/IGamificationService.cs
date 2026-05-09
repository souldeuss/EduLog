namespace EduLog.Services
{
    public class LevelInfo
    {
        public int Level { get; set; }
        public string Title { get; set; } = string.Empty;
        public int XpRequired { get; set; }
        public int XpForNextLevel { get; set; }
    }

    public class GamificationReward
    {
        public int XpAwarded { get; set; }
        public int CoinsAwarded { get; set; }
        public bool LeveledUp { get; set; }
        public int NewLevel { get; set; }
        public string? LevelTitle { get; set; }
    }

    public interface IGamificationService
    {
        IReadOnlyList<LevelInfo> Levels { get; }

        LevelInfo GetLevelInfo(int level);
        LevelInfo GetLevelByXp(int totalXp);
        int GetXpForNextLevel(int currentLevel);

        Task<GamificationReward> ProcessGradeAddedAsync(int studentId, int gradeValue);
        Task<GamificationReward> ProcessHomeworkSubmittedAsync(int studentId, int lessonMaterialId, bool onTime);
        Task<GamificationReward> RecalculateStreakAsync(int studentId, DateTime date);

        Task<string> GetGradeAverageStatusAsync(int studentId);
        Task<double> GetGradeAverageAsync(int studentId, int days = 30);
    }
}
