using CompetitionForBusiness.Data;
using CompetitionForBusiness.Hubs;
using CompetitionForBusiness.Models;
using CompetitionForBusiness.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

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

        // Katılımcı Kaydı (Mobil Uygulama Giriş Ettiğinde)
        [HttpPost("register")]
            public async Task<IActionResult> RegisterParticipant([FromBody] Participant participant)
            {
                participant.Id = Guid.NewGuid();
                participant.CreatedAt = DateTime.UtcNow;

                _context.Participants.Add(participant);
                await _context.SaveChangesAsync();

                return Ok(participant);
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
        }
}
