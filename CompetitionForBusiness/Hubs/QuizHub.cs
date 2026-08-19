using CompetitionForBusiness.Data;
using CompetitionForBusiness.Models;

using Microsoft.AspNetCore.SignalR;

using System;

namespace CompetitionForBusiness.Hubs
{
    public class QuizHub:Hub
    {
        private readonly AppDbContext _context;

        public QuizHub(AppDbContext context)
        {
            _context = context;
        }

        // 1. Kullanıcı Yarışma Odasına Katıldığında Çalışır
        public async Task JoinRoom(string roomId, string participantName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

            // Odadaki diğer katılımcılara bilgi verilir
            await Clients.Group(roomId).SendAsync("UserJoined", $"{participantName} odaya katıldı.");
        }

        // 2. Yarışmayı Başlatma ve Soruyu Tüm Katılımcılara Basma (Yönetici Tetikler)
        public async Task SendNextQuestion(string roomId, int questionId)
        {
            var question = await _context.Questions.FindAsync(questionId);

            if (question != null)
            {
                // Şıkların doğru cevabını mobil istemciye GÖNDERMİYORUZ (Güvenlik)
                var questionData = new
                {
                    question.Id,
                    question.QuestionText,
                    question.OptionA,
                    question.OptionB,
                    question.OptionC,
                    question.OptionD,
                    question.Category,
                    DurationSeconds = 15 // Her soru için 15 saniye süre
                };

                // Odaya bağlı tüm 200-300 kişiye soruyu aynı anda push eder
                await Clients.Group(roomId).SendAsync("ReceiveQuestion", questionData);
            }
        }

        // 3. Kullanıcı Cevabını Gönderdiğinde Çalışır
        public async Task SubmitAnswer(Guid participantId, int questionId, string selectedOption, int responseTimeMs)
        {
            var answer = new UserAnswer
            {
                ParticipantId = participantId,
                QuestionId = questionId,
                SelectedOption = selectedOption,
                ResponseTimeMs = responseTimeMs,
                CreatedAt = DateTime.UtcNow
            };

            _context.UserAnswers.Add(answer);
            await _context.SaveChangesAsync();

            // İstemciye cevabın alındığı onaylanır
            await Clients.Caller.SendAsync("AnswerSubmitted", true);
        }

        // 4. Bağlantı Koptuğunda
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
        }
    }
}

