using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;

namespace EduFlyUp.Repositories
{
    public class QuizRepository : IQuizRepository
    {
        public async Task<IEnumerable<Quiz>> GetByCourseIdAsync(int courseId) =>
            await QuizDAO.Instance.GetByCourseIdAsync(courseId);

        public async Task<Quiz?> GetByIdAsync(int id) =>
            await QuizDAO.Instance.GetByIdAsync(id);

        public async Task<Quiz?> GetByIdWithQuestionsAsync(int id) =>
            await QuizDAO.Instance.GetByIdWithQuestionsAsync(id);

        public async Task<Quiz> AddAsync(Quiz quiz) =>
            await QuizDAO.Instance.AddAsync(quiz);

        public async Task UpdateAsync(Quiz quiz) =>
            await QuizDAO.Instance.UpdateAsync(quiz);

        public async Task DeleteAsync(int id) =>
            await QuizDAO.Instance.DeleteAsync(id);

        public async Task AddQuestionToQuizAsync(int quizId, int questionId, int order = 1) =>
            await QuizDAO.Instance.AddQuestionToQuizAsync(quizId, questionId, order);

        public async Task RemoveQuestionFromQuizAsync(int quizId, int questionId) =>
            await QuizDAO.Instance.RemoveQuestionFromQuizAsync(quizId, questionId);

        public async Task SetQuestionsForQuizAsync(int quizId, IEnumerable<int> questionIds) =>
            await QuizDAO.Instance.SetQuestionsForQuizAsync(quizId, questionIds);

        public async Task<int> CountByCourseIdAsync(int courseId) =>
            await QuizDAO.Instance.CountByCourseIdAsync(courseId);
    }
}
