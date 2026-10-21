using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;

namespace EduFlyUp.Repositories
{
    public class QuestionRepository : IQuestionRepository
    {
        public async Task<IEnumerable<Question>> GetByCourseIdAsync(int courseId, QuestionDifficulty? difficulty = null, string? search = null) =>
            await QuestionDAO.Instance.GetByCourseIdAsync(courseId, difficulty, search);

        public async Task<Question?> GetByIdAsync(int id) =>
            await QuestionDAO.Instance.GetByIdAsync(id);

        public async Task<Question> AddAsync(Question question) =>
            await QuestionDAO.Instance.AddAsync(question);

        public async Task UpdateAsync(Question question, IEnumerable<QuestionOption> options) =>
            await QuestionDAO.Instance.UpdateAsync(question, options);

        public async Task DeleteAsync(int id) =>
            await QuestionDAO.Instance.DeleteAsync(id);

        public async Task<int> CountByCourseIdAsync(int courseId) =>
            await QuestionDAO.Instance.CountByCourseIdAsync(courseId);

        public async Task<IEnumerable<Question>> GetByIdsAsync(IEnumerable<int> ids) =>
            await QuestionDAO.Instance.GetByIdsAsync(ids);
    }
}
