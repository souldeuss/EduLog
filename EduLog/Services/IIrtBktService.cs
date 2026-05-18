using EduLog.Models;

namespace EduLog.Services
{
    // Сервіс адаптивного тестування: IRT 3PL + Bayesian Knowledge Tracing.
    public interface IIrtBktService
    {
        // Ймовірність правильної відповіді за моделлю IRT 3PL.
        // theta — поточний рівень здібностей учня; a/b/c — параметри питання.
        double IrtProbability(double theta, double a, double b, double c);

        // Оновлення BKT P(L) після спостереження правильної/неправильної відповіді.
        // pL — попередня P(L); pT — P(transit), pS — P(slip), pG — P(guess).
        double UpdateBkt(double pL, double pT, double pS, double pG, bool isCorrect);

        // Підбір наступного питання з банку згідно з поточним pL.
        QuestionItem? SelectNextQuestion(double pL, List<QuestionItem> available);

        // Поточний рівень theta учня, оцінений з ймовірності засвоєння теми.
        // Грубе монотонне відображення pL [0..1] → theta [-3..+3].
        double EstimateTheta(double pL);

        // Стандартні дефолти BKT, використовуються в контролерах.
        double DefaultPT { get; }
        double DefaultPS { get; }
        double DefaultPG { get; }
    }
}
