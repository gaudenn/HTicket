using HTicket.Models;

namespace HTicket.Services
{
    public interface IChatbotService
    {
        Task<string> GetReplyAsync(string userQuestion, string customerId, List<ChatItem> history);

    }
}