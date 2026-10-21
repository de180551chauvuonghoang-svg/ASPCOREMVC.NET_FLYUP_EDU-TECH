using EduFlyUp.BusinessObjects;
using EduFlyUp.Repositories;

namespace EduFlyUp.Services
{
    public interface IQuestionService
    {
        Task<IEnumerable<Question>> GetQuestionsByCourseIdAsync(int courseId, QuestionDifficulty? difficulty = null, string? search = null);
        Task<Question?> GetQuestionByIdAsync(int id);
        Task<(bool Success, string Message, Question? Data)> CreateQuestionAsync(Question question, List<QuestionOption> options);
        Task<(bool Success, string Message)> UpdateQuestionAsync(Question question, List<QuestionOption> options);
        Task<bool> DeleteQuestionAsync(int id);
        Task<int> GetTotalQuestionsCountByCourseIdAsync(int courseId);
    }

    public class QuestionService : IQuestionService
    {
        private readonly IQuestionRepository _questionRepository;

        public QuestionService(IQuestionRepository questionRepository)
        {
            _questionRepository = questionRepository;
        }

        public async Task<IEnumerable<Question>> GetQuestionsByCourseIdAsync(
            int courseId, 
            QuestionDifficulty? difficulty = null, 
            string? search = null) =>
            await _questionRepository.GetByCourseIdAsync(courseId, difficulty, search);

        public async Task<Question?> GetQuestionByIdAsync(int id) =>
            await _questionRepository.GetByIdAsync(id);

        public async Task<(bool Success, string Message, Question? Data)> CreateQuestionAsync(
            Question question, 
            List<QuestionOption> options)
        {
            var (valid, message) = ValidateQuestionAndOptions(question, options);
            if (!valid)
            {
                return (false, message, null);
            }

            question.Options = options;
            var created = await _questionRepository.AddAsync(question);
            return (true, "Tạo câu hỏi mới thành công.", created);
        }

        public async Task<(bool Success, string Message)> UpdateQuestionAsync(
            Question question, 
            List<QuestionOption> options)
        {
            var (valid, message) = ValidateQuestionAndOptions(question, options);
            if (!valid)
            {
                return (false, message);
            }

            var existing = await _questionRepository.GetByIdAsync(question.Id);
            if (existing == null)
            {
                return (false, "Không tìm thấy câu hỏi cần cập nhật.");
            }

            await _questionRepository.UpdateAsync(question, options);
            return (true, "Cập nhật câu hỏi thành công.");
        }

        public async Task<bool> DeleteQuestionAsync(int id)
        {
            var existing = await _questionRepository.GetByIdAsync(id);
            if (existing == null) return false;

            await _questionRepository.DeleteAsync(id);
            return true;
        }

        public async Task<int> GetTotalQuestionsCountByCourseIdAsync(int courseId) =>
            await _questionRepository.CountByCourseIdAsync(courseId);

        private (bool IsValid, string ErrorMessage) ValidateQuestionAndOptions(
            Question question, 
            List<QuestionOption> options)
        {
            if (string.IsNullOrWhiteSpace(question.Content))
            {
                return (false, "Nội dung câu hỏi không được để trống.");
            }

            if (question.CourseId <= 0)
            {
                return (false, "Câu hỏi phải thuộc về một khóa học hợp lệ.");
            }

            if (options == null || options.Count < 2)
            {
                return (false, "Câu hỏi trắc nghiệm phải có ít nhất 2 lựa chọn đáp án.");
            }

            if (options.Count > 6)
            {
                return (false, "Câu hỏi trắc nghiệm tối đa 6 lựa chọn đáp án.");
            }

            if (options.Any(o => string.IsNullOrWhiteSpace(o.Text)))
            {
                return (false, "Nội dung các phương án đáp án không được để trống.");
            }

            if (!options.Any(o => o.IsCorrect))
            {
                return (false, "Phải có ít nhất 1 phương án đáp án đúng.");
            }

            if (question.Points <= 0)
            {
                question.Points = 1;
            }

            return (true, string.Empty);
        }
    }
}
