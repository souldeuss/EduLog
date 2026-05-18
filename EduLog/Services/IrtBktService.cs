using EduLog.Models;

namespace EduLog.Services
{
    public class IrtBktService : IIrtBktService
    {
        // Стандартні параметри BKT (за літературою Corbett & Anderson, типові значення).
        public double DefaultPT => 0.1;   // P(transit) — ймовірність вивчити тему за крок
        public double DefaultPS => 0.1;   // P(slip)    — помилитися, знаючи матеріал
        public double DefaultPG => 0.2;   // P(guess)   — вгадати, не знаючи матеріал

        // IRT 3PL: P(correct | theta) = c + (1 - c) * sigmoid(1.7 * a * (theta - b)).
        // Константа 1.702 — стандартне наближення нормальної моделі логістичною.
        public double IrtProbability(double theta, double a, double b, double c)
        {
            c = Math.Clamp(c, 0.0, 0.35);
            double z = 1.702 * a * (theta - b);
            double sigmoid = 1.0 / (1.0 + Math.Exp(-z));
            return c + (1.0 - c) * sigmoid;
        }

        // Класичне оновлення BKT за Corbett & Anderson:
        //   1) посеред-крокова постеріорна P(L | спостереження)
        //   2) P(L_{n+1}) = P(L | obs) + (1 - P(L | obs)) * P(T)
        public double UpdateBkt(double pL, double pT, double pS, double pG, bool isCorrect)
        {
            pL = Math.Clamp(pL, 1e-6, 1.0 - 1e-6);
            pT = Math.Clamp(pT, 0.0, 1.0);
            pS = Math.Clamp(pS, 0.0, 1.0);
            pG = Math.Clamp(pG, 0.0, 1.0);

            double posterior;
            if (isCorrect)
            {
                // P(L | correct) = P(L)(1-P(S)) / [ P(L)(1-P(S)) + (1-P(L))P(G) ]
                double num = pL * (1.0 - pS);
                double den = num + (1.0 - pL) * pG;
                posterior = den <= 0 ? pL : num / den;
            }
            else
            {
                // P(L | wrong) = P(L) P(S) / [ P(L) P(S) + (1-P(L))(1-P(G)) ]
                double num = pL * pS;
                double den = num + (1.0 - pL) * (1.0 - pG);
                posterior = den <= 0 ? pL : num / den;
            }

            double updated = posterior + (1.0 - posterior) * pT;
            return Math.Clamp(updated, 0.0, 1.0);
        }

        // Грубе монотонне відображення pL → theta. pL=0.5 ≈ нейтральний рівень 0.
        public double EstimateTheta(double pL)
        {
            pL = Math.Clamp(pL, 1e-4, 1.0 - 1e-4);
            // logit, масштабований у [-3..+3] (logit(0.95)≈2.94)
            double logit = Math.Log(pL / (1.0 - pL));
            return Math.Clamp(logit, -3.0, 3.0);
        }

        public QuestionItem? SelectNextQuestion(double pL, List<QuestionItem> available)
        {
            if (available == null || available.Count == 0) return null;

            double theta = EstimateTheta(pL);

            // Сильний учень — підкидаємо складніше питання, щоб зростав виклик.
            if (pL > 0.8)
            {
                var harder = available
                    .Where(q => q.IrtB > theta + 0.5)
                    .OrderBy(q => q.IrtB)
                    .FirstOrDefault();
                if (harder != null) return harder;
            }
            // Слабкий учень — простіше питання (з підказкою, якщо є).
            else if (pL < 0.4)
            {
                var easier = available
                    .Where(q => q.IrtB < theta - 0.5)
                    .OrderByDescending(q => q.IrtB)
                    .FirstOrDefault();
                if (easier != null) return easier;
            }

            // Інакше — найближче по складності до поточного рівня.
            return available
                .OrderBy(q => Math.Abs(q.IrtB - theta))
                .First();
        }
    }
}
