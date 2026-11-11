using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Repositories
{
    public interface IQuizRepository
    {
        Task<IEnumerable<Quiz>> GetByCourseIdAsync(int courseId);
        Task<Quiz?> GetByIdAsync(int id);
        Task<Quiz?> GetByIdWithQuestionsAsync(int id);
        Task<Quiz> AddAsync(Quiz quiz);
        Task UpdateAsync(Quiz quiz);
        Task DeleteAsync(int id);
        Task SetQuestionsForQuizAsync(int quizId, IEnumerable<int> questionIds);
        Task<int> CountByCourseIdAsync(int courseId);
    }
}
