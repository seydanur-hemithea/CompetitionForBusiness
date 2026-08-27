using CompetitionForBusiness.Data;
using CompetitionForBusiness.Hubs;
using CompetitionForBusiness.Models;
using CompetitionForBusiness.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq; // LINQ işlemleri (Select, ToListAsync) için şart
using System.Threading.Tasks;

namespace CompetitionForBusiness.Controllers
{
    public class RegisterDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? FullName { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
    }

    public class SubmitAnswerDto
    {
        public Guid ParticipantId { get; set; }
        public int QuestionId { get; set; }
        public string SelectedOption { get; set; } = string.Empty;
        public int ResponseTimeMs { get; set; }
    }

    public class QuizSubmissionDto
    {
        public Guid ParticipantId { get; set; }
        public int Score { get; set; }
    }

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

        // 1. Kayıt Endpoint'i
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto model)
        {
            Console.WriteLine($"[RENDER LOG] /register çağrıldı: {model?.Email}");

            if (model == null || string.IsNullOrWhiteSpace(model.Email))
            {
                Console.WriteLine("[RENDER HATA] Register: Geçersiz model veya boş email.");
                return BadRequest("Geçersiz kayıt verisi.");
            }

            try
            {
                string combinedFullName = string.IsNullOrWhiteSpace(model.FullName)
                    ? $"{model.FirstName} {model.LastName}".Trim()
                    : model.FullName;

                var participant = new Participant
                {
                    Id = Guid.NewGuid(),
                    Email = model.Email,
                    Phone = model.Phone ?? string.Empty,
                    FullName = combinedFullName,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Participants.Add(participant);
                await _context.SaveChangesAsync();

                Console.WriteLine($"[RENDER LOG] Kullanıcı başarıyla kaydedildi -> ID: {participant.Id}");

                return Ok(new
                {
                    id = participant.Id,
                    fullName = participant.FullName,
                    email = participant.Email,
                    phone = participant.Phone,
                    createdAt = participant.CreatedAt
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RENDER EXCEPTION] Register Hatası: {ex.Message}\n{ex.StackTrace}");
                return StatusCode(500, $"Kayıt hatası: {ex.Message}");
            }
        }

        // 2. Soruları Getir
        [HttpGet("questions")]
        public async Task<IActionResult> GetQuestions()
        {
            Console.WriteLine("[RENDER LOG] /questions isteği ulaştı.");
            try
            {
                var questions = await _context.Questions
                    .AsNoTracking()
                    .Select(q => new
                    {
                        id = q.Id,
                        questionText = q.QuestionText,
                        optionA = q.OptionA,
                        optionB = q.OptionB,
                        optionC = q.OptionC,
                        optionD = q.OptionD,
                        category = q.Category
                    })
                    .ToListAsync();

                Console.WriteLine($"[RENDER LOG] Dönen soru sayısı: {questions.Count}");
                return Ok(questions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RENDER EXCEPTION] Questions Çekilirken Hata: {ex.Message}\n{ex.StackTrace}");
                return StatusCode(500, $"Soru getirme hatası: {ex.Message}");
            }
        }

        // 3. Tekil Soru Cevabı Kaydetme
        [HttpPost("submit-answer")]
        public async Task<IActionResult> SubmitAnswer([FromBody] SubmitAnswerDto dto)
        {
            Console.WriteLine($"[RENDER LOG] /submit-answer çağrıldı. Katılımcı: {dto?.ParticipantId}");
            try
            {
                if (dto == null) return BadRequest("Cevap verisi boş olamaz.");

                var answer = new UserAnswer
                {
                    ParticipantId = dto.ParticipantId,
                    QuestionId = dto.QuestionId,
                    SelectedOption = dto.SelectedOption,
                    ResponseTimeMs = dto.ResponseTimeMs,
                    CreatedAt = DateTime.UtcNow
                };

                _context.UserAnswers.Add(answer);
                await _context.SaveChangesAsync();

                Console.WriteLine($"[RENDER LOG] Cevap başarıyla kaydedildi. AnswerID: {answer.Id}");
                return Ok(new { message = "Yanıt başarıyla kaydedildi.", answerId = answer.Id });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RENDER EXCEPTION] SubmitAnswer Hatası: {ex.Message}\n{ex.StackTrace}");
                return StatusCode(500, $"Cevap kaydetme hatası: {ex.Message}");
            }
        }

        // 4. Genel Quiz Puanı Kaydetme
        [HttpPost("submit")]
        public async Task<IActionResult> SubmitQuiz([FromBody] QuizSubmissionDto model)
        {
            Console.WriteLine($"[RENDER LOG] /submit çağrıldı. Katılımcı ID: {model?.ParticipantId}");
            try
            {
                if (model == null) return BadRequest("Quiz verisi boş olamaz.");

                var participant = await _context.Participants.FindAsync(model.ParticipantId);
                if (participant != null)
                {
                    await _context.SaveChangesAsync();
                }

                Console.WriteLine($"[RENDER LOG] Quiz başarıyla tamamlandı.");
                return Ok(new { message = "Puan başarıyla kaydedildi." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RENDER EXCEPTION] Submit Hatası: {ex.Message}\n{ex.StackTrace}");
                return StatusCode(500, $"Kayıt hatası: {ex.Message}");
            }
        }

        // 5. Yapay Zeka Analiz Endpoint'i
        [HttpGet("analyze-result/{participantId}")]
        public async Task<IActionResult> GetAnalysisResult(Guid participantId)
        {
            Console.WriteLine($"[RENDER LOG] /analyze-result çağrıldı. ParticipantID: {participantId}");
            try
            {
                var result = await _aiService.AnalyzeParticipantPerformanceAsync(participantId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RENDER EXCEPTION] Yapay zeka analiz hatası: {ex.Message}\n{ex.StackTrace}");
                return StatusCode(500, $"Analiz alınamadı: {ex.Message}");
            }
        }

        // 6. Yardımcı Soru Ekleme Endpoint'i
        [HttpPost("add-question")]
        public async Task<IActionResult> AddQuestion([FromBody] Question question)
        {
            Console.WriteLine($"[RENDER LOG] /add-question çağrıldı.");
            try
            {
                _context.Questions.Add(question);
                await _context.SaveChangesAsync();
                return Ok(question);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RENDER EXCEPTION] AddQuestion Hatası: {ex.Message}\n{ex.StackTrace}");
                return StatusCode(500, $"Soru ekleme hatası: {ex.Message}");
            }
        }

        // 7. SignalR Yayın Endpoint'i
        [HttpPost("broadcast-question/{roomId}/{questionId}")]
        public async Task<IActionResult> BroadcastQuestion(string roomId, int questionId)
        {
            Console.WriteLine($"[RENDER LOG] /broadcast-question çağrıldı. Room: {roomId}, Question: {questionId}");
            try
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

                await _hubContext.Clients.Group(roomId).SendAsync("ReceiveQuestion", questionData);
                return Ok(new { Message = "Soru tüm katılımcılara gönderildi." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RENDER EXCEPTION] BroadcastQuestion Hatası: {ex.Message}\n{ex.StackTrace}");
                return StatusCode(500, $"Yayın hatası: {ex.Message}");
            }
        }
    }
}
