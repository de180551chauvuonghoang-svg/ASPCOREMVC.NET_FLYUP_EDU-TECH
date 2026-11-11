using EduFlyUp.BusinessObjects;
using Microsoft.EntityFrameworkCore;

namespace EduFlyUp.DataAccess
{
    public class QuizDAO : SingletonBase<QuizDAO>
    {
        public async Task<IEnumerable<Quiz>> GetByCourseIdAsync(int courseId)
        {
            using var context = new AppDbContext();
            return await context.Quizzes
                .Include(q => q.QuizQuestions)
                .Where(q => q.CourseId == courseId)
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync();
        }

        public async Task<Quiz?> GetByIdAsync(int id)
        {
            using var context = new AppDbContext();
            return await context.Quizzes.FindAsync(id);
        }

        public async Task<Quiz?> GetByIdWithQuestionsAsync(int id)
        {
            using var context = new AppDbContext();
            return await context.Quizzes
                .Include(q => q.QuizQuestions.OrderBy(qq => qq.Order))
                    .ThenInclude(qq => qq.Question)
                        .ThenInclude(ques => ques.Options)
                .FirstOrDefaultAsync(q => q.Id == id);
        }

        public async Task<Quiz> AddAsync(Quiz quiz)
        {
            using var context = new AppDbContext();
            await context.Quizzes.AddAsync(quiz);
            await context.SaveChangesAsync();
            return quiz;
        }

        public async Task UpdateAsync(Quiz quiz)
        {
            using var context = new AppDbContext();
            var existingQuiz = await context.Quizzes.FindAsync(quiz.Id);
            if (existingQuiz != null)
            {
                existingQuiz.Title = quiz.Title;
                existingQuiz.Description = quiz.Description;
                existingQuiz.DurationMinutes = quiz.DurationMinutes;
                existingQuiz.PassingScorePercentage = quiz.PassingScorePercentage;
                existingQuiz.IsCAT = quiz.IsCAT;

                await context.SaveChangesAsync();
            }
        }

        public async Task DeleteAsync(int id)
        {
            using var context = new AppDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();
            var quiz = await context.Quizzes
                .Include(q => q.QuizQuestions)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (quiz != null)
            {
                if (await context.QuizAttempts.AnyAsync(a => a.QuizId == id))
                {
                    throw new InvalidOperationException("Không thể xóa đề thi đã có học viên tham gia làm bài.");
                }

                if (quiz.QuizQuestions.Any())
                {
                    context.QuizQuestions.RemoveRange(quiz.QuizQuestions);
                }

                context.Quizzes.Remove(quiz);
                await context.SaveChangesAsync();
            }

            await transaction.CommitAsync();
        }

        public async Task SetQuestionsForQuizAsync(int quizId, IEnumerable<int> questionIds)
        {
            using var context = new AppDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();
            var existingItems = await context.QuizQuestions
                .Where(qq => qq.QuizId == quizId)
                .ToListAsync();

            context.QuizQuestions.RemoveRange(existingItems);

            int order = 1;
            foreach (var qId in questionIds.Distinct())
            {
                context.QuizQuestions.Add(new QuizQuestion
                {
                    QuizId = quizId,
                    QuestionId = qId,
                    Order = order++
                });
            }

            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        public async Task<int> CountByCourseIdAsync(int courseId)
        {
            using var context = new AppDbContext();
            return await context.Quizzes.CountAsync(q => q.CourseId == courseId);
        }
    }
}
