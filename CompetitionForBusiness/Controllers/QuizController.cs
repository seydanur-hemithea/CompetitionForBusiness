using CompetitionForBusiness.Data;
using CompetitionForBusiness.Hubs;
using CompetitionForBusiness.Models;
using CompetitionForBusiness.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore; // <-- BU SATIR EKLENDİ (AsNoTracking ve ToListAsync için şart)
using System;
using System.Threading.Tasks;

using System;

namespace CompetitionForBusiness.Controllers
{
 
        
        [ApiController]
        [Route("api/[controller]")]
        public class QuizController : ControllerBase
        {
            private readonly AppDbContext _context;
            private readonly IHubContext<QuizHub> _hubContext;
            private readonly AiAnalysisService _aiService;

            public QuizController(AppDbContext context, IHubContext<QuizHub> hubContext, AiAnalysisService aiService)
            {
                _context = context;
                _hubContext = hubContext;
                _aiService = aiService;
            }
        // Aday Sınavı Bitirdiğinde Yapay Zeka Analizini Döndüren Endpoint
        [HttpGet("analyze-result/{participantId}")]
        public async Task<IActionResult> GetAnalysisResult(Guid participantId)
        {
            var result = await _aiService.AnalyzeParticipantPerformanceAsync(participantId);
            return Ok(result);
        }

       [HttpPost("register")]
public async Task<IActionResult> Register([FromBody] RegisterDto model)
{
    Console.WriteLine($"[LOG] Kayıt isteği geldi: {model.Email}");

    // Ad ve Soyadı birleştirip FullName alanına yazıyoruz (fullName boş kalmasın diye)
    string combinedFullName = string.IsNullOrWhiteSpace(model.FullName) 
        ? $"{model.FirstName} {model.LastName}".Trim() 
        : model.FullName;

    var participant = new Participant
    {
        Id = Guid.NewGuid(),
        Email = model.Email,
        Phone = model.Phone,
        FullName = combinedFullName, // Artık veritabanında dolu görünecek!
        CreatedAt = DateTime.UtcNow
    };

    _context.Participants.Add(participant);
    await _context.SaveChangesAsync();

    Console.WriteLine($"[LOG] Kullanıcı başarıyla kaydedildi ID: {participant.Id}");

    return Ok(new
    {
        id = participant.Id,
        fullName = participant.FullName,
        email = participant.Email,
        phone = participant.Phone,
        createdAt = participant.CreatedAt
    });
}
            // Örnek Soru Ekleme (Yarışma İçin Sorular)
            [HttpPost("add-question")]
            public async Task<IActionResult> AddQuestion([FromBody] Question question)
            {
                _context.Questions.Add(question);
                await _context.SaveChangesAsync();
                return Ok(question);
            }

            // Yarışmayı Başlat / Sonraki Soruyu Odadaki Herkese Gönder
            [HttpPost("broadcast-question/{roomId}/{questionId}")]
            public async Task<IActionResult> BroadcastQuestion(string roomId, int questionId)
            {
                var question = await _context.Questions.FindAsync(questionId);
                if (question == null) return NotFound("Soru bulunamadı.");

                var questionData = new
                {
                    question.Id,
                    question.QuestionText,
                    question.OptionA,
                    question.OptionB,
                    question.OptionC,
                    question.OptionD,
                    question.Category,
                    DurationSeconds = 15
                };

                // SignalR ile anlık 300 kişiye aynı anda gönderir
                await _hubContext.Clients.Group(roomId).SendAsync("ReceiveQuestion", questionData);

                return Ok(new { Message = "Soru tüm katılımcılara gönderildi." });
            }
            [HttpPost("submit-answer")]
public async Task<IActionResult> SubmitAnswer([FromBody] UserAnswer answer)
{
    // Id identity/auto-increment olduğu için 0 geldiğinde sıfırlayabilirsiniz
    // veya EF Core'un otomatik ID üretmesine izin verebilirsiniz.
    if (answer.Id == 0)
    {
        // EF Core SaveChangesAsync sırasında PostgreSQL IDENTITY kolonu için ID'yi otomatik atayacaktır.
    }

    _context.UserAnswers.Add(answer);
    await _context.SaveChangesAsync();

    return Ok(new { message = "Yanıt başarıyla kaydedildi.", answerId = answer.Id });
}
   // Tüm soruları getiren yeni endpoint
  [HttpGet("questions")]
public async Task<IActionResult> GetQuestions()
{
    Console.WriteLine("[LOG] /questions endpoint'ine istek geldi.");

    try
    {
        // Supabase/EF Core'dan soruları çekiyoruz
        var questions = await _context.Questions.ToListAsync();
        Console.WriteLine($"[LOG] Veritabanından çekilen soru sayısı: {questions.Count}");

        return Ok(questions);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[HATA] Questions çekilirken hata oluştu: {ex.Message}");
        return StatusCode(500, $"Veritabanı hatası: {ex.Message}");
    }
}
        }
        }

