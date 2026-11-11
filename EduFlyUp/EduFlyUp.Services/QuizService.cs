using EduFlyUp.BusinessObjects;
using EduFlyUp.Repositories;

namespace EduFlyUp.Services
{
    public interface IQuizService
    {
        Task<IEnumerable<Quiz>> GetQuizzesByCourseIdAsync(int courseId);
        Task<Quiz?> GetQuizByIdAsync(int id);
        Task<Quiz?> GetQuizWithQuestionsAsync(int id);
        Task<(bool Success, string Message, Quiz? Data)> CreateQuizAsync(Quiz quiz);
        Task<(bool Success, string Message)> UpdateQuizAsync(Quiz quiz);
        Task<bool> DeleteQuizAsync(int id);
        Task<(bool Success, string Message)> AssignQuestionsToQuizAsync(int quizId, IEnumerable<int> questionIds);
        Task<int> GetTotalQuizzesCountByCourseIdAsync(int courseId);
    }

    public class QuizService : IQuizService
    {
        private readonly IQuizRepository _quizRepository;
        private readonly IQuestionRepository _questionRepository;

        public QuizService(IQuizRepository quizRepository, IQuestionRepository questionRepository)
        {
            _quizRepository = quizRepository;
            _questionRepository = questionRepository;
        }

        public async Task<IEnumerable<Quiz>> GetQuizzesByCourseIdAsync(int courseId) =>
            await _quizRepository.GetByCourseIdAsync(courseId);

        public async Task<Quiz?> GetQuizByIdAsync(int id) =>
            await _quizRepository.GetByIdAsync(id);

        public async Task<Quiz?> GetQuizWithQuestionsAsync(int id) =>
            await _quizRepository.GetByIdWithQuestionsAsync(id);

        public async Task<(bool Success, string Message, Quiz? Data)> CreateQuizAsync(Quiz quiz)
        {
            var (valid, message) = ValidateQuiz(quiz);
            if (!valid)
            {
                return (false, message, null);
            }

            var created = await _quizRepository.AddAsync(quiz);
            return (true, "Tạo đề thi mới thành công.", created);
        }

        public async Task<(bool Success, string Message)> UpdateQuizAsync(Quiz quiz)
        {
            var (valid, message) = ValidateQuiz(quiz);
            if (!valid)
            {
                return (false, message);
            }

            var existing = await _quizRepository.GetByIdAsync(quiz.Id);
            if (existing == null)
            {
                return (false, "Không tìm thấy đề thi cần cập nhật.");
            }

            await _quizRepository.UpdateAsync(quiz);
            return (true, "Cập nhật đề thi thành công.");
        }

        public async Task<bool> DeleteQuizAsync(int id)
        {
            var existing = await _quizRepository.GetByIdAsync(id);
            if (existing == null) return false;

            await _quizRepository.DeleteAsync(id);
            return true;
        }

        public async Task<(bool Success, string Message)> AssignQuestionsToQuizAsync(int quizId, IEnumerable<int> questionIds)
        {
            var quiz = await _quizRepository.GetByIdAsync(quizId);
            if (quiz == null)
            {
                return (false, "Không tìm thấy đề thi.");
            }

            var distinctIds = questionIds.Where(id => id > 0).Distinct().ToList();
            if (distinctIds.Any())
            {
                var questions = (await _questionRepository.GetByIdsAsync(distinctIds)).ToList();
                if (questions.Count != distinctIds.Count || questions.Any(q => q.CourseId != quiz.CourseId))
                {
                    return (false, "Một số câu hỏi được chọn không tồn tại hoặc không thuộc khóa học này.");
                }
            }

            await _quizRepository.SetQuestionsForQuizAsync(quizId, distinctIds);
            return (true, "Cập nhật danh sách câu hỏi trong đề thi thành công.");
        }

        public async Task<int> GetTotalQuizzesCountByCourseIdAsync(int courseId) =>
            await _quizRepository.CountByCourseIdAsync(courseId);

        private (bool IsValid, string ErrorMessage) ValidateQuiz(Quiz quiz)
        {
            if (string.IsNullOrWhiteSpace(quiz.Title))
            {
                return (false, "Tiêu đề đề thi không được để trống.");
            }

            if (quiz.CourseId <= 0)
            {
                return (false, "Đề thi phải gắn với một khóa học hợp lệ.");
            }

            if (quiz.DurationMinutes <= 0 || quiz.DurationMinutes > 300)
            {
                return (false, "Thời gian làm bài phải từ 1 đến 300 phút.");
            }

            if (quiz.PassingScorePercentage <= 0 || quiz.PassingScorePercentage > 100)
            {
                return (false, "Điểm phần trăm đạt yêu cầu phải từ 1% đến 100%.");
            }

            return (true, string.Empty);
        }
    }
}
