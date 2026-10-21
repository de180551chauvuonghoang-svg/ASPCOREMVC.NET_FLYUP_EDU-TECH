using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Repositories
{
    public interface IQuestionRepository
    {
        Task<IEnumerable<Question>> GetByCourseIdAsync(int courseId, QuestionDifficulty? difficulty = null, string? search = null);
        Task<Question?> GetByIdAsync(int id);
        Task<Question> AddAsync(Question question);
        Task UpdateAsync(Question question, IEnumerable<QuestionOption> options);
        Task DeleteAsync(int id);
        Task<int> CountByCourseIdAsync(int courseId);
        Task<IEnumerable<Question>> GetByIdsAsync(IEnumerable<int> ids);
    }
}
