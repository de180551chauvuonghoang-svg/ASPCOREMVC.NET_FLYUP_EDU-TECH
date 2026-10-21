using EduFlyUp.BusinessObjects;
using Microsoft.EntityFrameworkCore;

namespace EduFlyUp.DataAccess
{
    public class QuestionDAO : SingletonBase<QuestionDAO>
    {
        public async Task<IEnumerable<Question>> GetByCourseIdAsync(
            int courseId, 
            QuestionDifficulty? difficulty = null, 
            string? search = null)
        {
            using var context = new AppDbContext();
            var query = context.Questions
                .Include(q => q.Options)
                .Where(q => q.CourseId == courseId);

            if (difficulty.HasValue)
            {
                query = query.Where(q => q.Difficulty == difficulty.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.Trim();
                query = query.Where(q => q.Content.Contains(keyword) || q.Explanation.Contains(keyword));
            }

            return await query.OrderByDescending(q => q.CreatedAt).ToListAsync();
        }

        public async Task<Question?> GetByIdAsync(int id)
        {
            using var context = new AppDbContext();
            return await context.Questions
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == id);
        }

        public async Task<Question> AddAsync(Question question)
        {
            using var context = new AppDbContext();
            await context.Questions.AddAsync(question);
            await context.SaveChangesAsync();
            return question;
        }

        public async Task UpdateAsync(Question question, IEnumerable<QuestionOption> options)
        {
            using var context = new AppDbContext();
            var existingQuestion = await context.Questions
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == question.Id);

            if (existingQuestion != null)
            {
                existingQuestion.Content = question.Content;
                existingQuestion.Difficulty = question.Difficulty;
                existingQuestion.Explanation = question.Explanation;
                existingQuestion.Points = question.Points;

                // Remove existing options and add updated options
                context.QuestionOptions.RemoveRange(existingQuestion.Options);

                foreach (var opt in options)
                {
                    existingQuestion.Options.Add(new QuestionOption
                    {
                        Text = opt.Text,
                        IsCorrect = opt.IsCorrect,
                        QuestionId = existingQuestion.Id
                    });
                }

                await context.SaveChangesAsync();
            }
        }

        public async Task DeleteAsync(int id)
        {
            using var context = new AppDbContext();
            var question = await context.Questions
                .Include(q => q.Options)
                .Include(q => q.QuizQuestions)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question != null)
            {
                if (question.QuizQuestions.Any())
                {
                    context.QuizQuestions.RemoveRange(question.QuizQuestions);
                }
                context.Questions.Remove(question);
                await context.SaveChangesAsync();
            }
        }

        public async Task<int> CountByCourseIdAsync(int courseId)
        {
            using var context = new AppDbContext();
            return await context.Questions.CountAsync(q => q.CourseId == courseId);
        }

        public async Task<IEnumerable<Question>> GetByIdsAsync(IEnumerable<int> ids)
        {
            using var context = new AppDbContext();
            return await context.Questions
                .Include(q => q.Options)
                .Where(q => ids.Contains(q.Id))
                .ToListAsync();
        }
    }
}
