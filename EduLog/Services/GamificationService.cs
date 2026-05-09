using EduLog.Data;
using EduLog.Models;
using Microsoft.EntityFrameworkCore;

namespace EduLog.Services
{
    public class GamificationService : IGamificationService
    {
        private readonly EduLogContext _context;

        private static readonly LevelInfo[] _levels = new[]
        {
            new LevelInfo { Level = 1, Title = "Новачок",   XpRequired = 0    },
            new LevelInfo { Level = 2, Title = "Учень",     XpRequired = 100  },
            new LevelInfo { Level = 3, Title = "Старанний", XpRequired = 300  },
            new LevelInfo { Level = 4, Title = "Знавець",   XpRequired = 700  },
            new LevelInfo { Level = 5, Title = "Відмінник", XpRequired = 1500 },
            new LevelInfo { Level = 6, Title = "Академік",  XpRequired = 3000 },
            new LevelInfo { Level = 7, Title = "Геній",     XpRequired = 6000 },
        };

        private const int LevelUpBonusCoins = 20;

        public GamificationService(EduLogContext context)
        {
            _context = context;
        }

        public IReadOnlyList<LevelInfo> Levels => _levels;

        public LevelInfo GetLevelInfo(int level)
        {
            var clamped = Math.Max(1, Math.Min(level, _levels.Length));
            return _levels[clamped - 1];
        }

        public LevelInfo GetLevelByXp(int totalXp)
        {
            LevelInfo current = _levels[0];
            for (int i = 0; i < _levels.Length; i++)
            {
                if (totalXp >= _levels[i].XpRequired)
                    current = _levels[i];
                else
                    break;
            }
            return current;
        }

        public int GetXpForNextLevel(int currentLevel)
        {
            if (currentLevel >= _levels.Length) return _levels[^1].XpRequired;
            return _levels[currentLevel].XpRequired; // _levels is 0-indexed; level N -> next is index N
        }

        public async Task<GamificationReward> ProcessGradeAddedAsync(int studentId, int gradeValue)
        {
            var (xp, coins) = gradeValue switch
            {
                >= 10 and <= 12 => (20, 5),
                >= 7 and <= 9   => (10, 2),
                >= 4 and <= 6   => (5, 1),
                _               => (0, 0)
            };
            return await AwardAsync(studentId, xp, coins, $"Оцінка {gradeValue}");
        }

        public async Task<GamificationReward> ProcessHomeworkSubmittedAsync(int studentId, int lessonMaterialId, bool onTime)
        {
            if (!onTime) return new GamificationReward();
            return await AwardAsync(studentId, 15, 3, "Здача ДЗ вчасно");
        }

        public async Task<GamificationReward> RecalculateStreakAsync(int studentId, DateTime date)
        {
            var student = await _context.Student.FirstOrDefaultAsync(s => s.Id == studentId);
            if (student == null) return new GamificationReward();

            var dayStart = date.Date;
            var dayEnd = dayStart.AddDays(1);

            var hasAbsenceToday = await _context.Absence
                .AnyAsync(a => a.StudentId == studentId && a.Date >= dayStart && a.Date < dayEnd);

            var totalReward = new GamificationReward();

            if (hasAbsenceToday)
            {
                student.AttendanceStreak = 0;
                student.LastAttendanceDate = null;
                await _context.SaveChangesAsync();
                return totalReward;
            }

            // Has full attendance today
            var lastDate = student.LastAttendanceDate?.Date;
            var yesterday = dayStart.AddDays(-1);

            if (lastDate == dayStart)
            {
                // Already counted today
                return totalReward;
            }

            if (lastDate == yesterday)
            {
                student.AttendanceStreak += 1;
            }
            else
            {
                student.AttendanceStreak = 1;
            }
            student.LastAttendanceDate = dayStart;

            await _context.SaveChangesAsync();

            // Milestone bonuses
            (int xp, int coins, string reason)? milestone = student.AttendanceStreak switch
            {
                3  => (10,  5,  "Стрік 3 дні"),
                7  => (25,  10, "Стрік 7 днів"),
                30 => (100, 30, "Стрік 30 днів"),
                _  => null
            };

            if (milestone.HasValue)
            {
                totalReward = await AwardAsync(studentId, milestone.Value.xp, milestone.Value.coins, milestone.Value.reason);
            }

            return totalReward;
        }

        public async Task<double> GetGradeAverageAsync(int studentId, int days = 30)
        {
            var since = DateTime.UtcNow.Date.AddDays(-days);
            var grades = await _context.Grade
                .Where(g => g.StudentId == studentId && g.Date >= since)
                .Select(g => g.Value)
                .ToListAsync();

            if (grades.Count == 0) return 0;
            return grades.Average();
        }

        public async Task<string> GetGradeAverageStatusAsync(int studentId)
        {
            var avg = await GetGradeAverageAsync(studentId);
            return avg switch
            {
                >= 10.0       => "✨ Ідеальний",
                >= 8.0        => "🌟 Чудовий",
                >= 6.0        => "👍 Добрий",
                >= 4.0        => "📚 Непогано",
                > 0           => "💪 Намагається",
                _             => "— Немає оцінок"
            };
        }

        private async Task<GamificationReward> AwardAsync(int studentId, int xp, int coins, string reason)
        {
            var reward = new GamificationReward { XpAwarded = xp, CoinsAwarded = coins };
            if (xp == 0 && coins == 0) return reward;

            var student = await _context.Student.FirstOrDefaultAsync(s => s.Id == studentId);
            if (student == null) return reward;

            var previousLevel = GetLevelByXp(student.ExperiencePoints).Level;

            student.ExperiencePoints += xp;
            student.EduCoins += coins;

            var newLevel = GetLevelByXp(student.ExperiencePoints);
            student.Level = newLevel.Level;

            if (coins != 0)
            {
                _context.CoinTransaction.Add(new CoinTransaction
                {
                    SchoolId = student.SchoolId,
                    StudentId = studentId,
                    Amount = coins,
                    Reason = reason,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // Level-up bonus
            if (newLevel.Level > previousLevel)
            {
                student.EduCoins += LevelUpBonusCoins;
                _context.CoinTransaction.Add(new CoinTransaction
                {
                    SchoolId = student.SchoolId,
                    StudentId = studentId,
                    Amount = LevelUpBonusCoins,
                    Reason = $"Рівень {newLevel.Level}: {newLevel.Title}",
                    CreatedAt = DateTime.UtcNow
                });
                reward.LeveledUp = true;
                reward.NewLevel = newLevel.Level;
                reward.LevelTitle = newLevel.Title;
                reward.CoinsAwarded += LevelUpBonusCoins;
            }

            await _context.SaveChangesAsync();
            return reward;
        }
    }
}
