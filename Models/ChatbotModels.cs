namespace HTicket.Models
    {
        // Class này dùng để hứng dữ liệu từ giao diện gửi lên (JSON)
        public class ChatRequest
        {
            public string Message { get; set; }
            public List<ChatItem> History { get; set; } = new List<ChatItem>();
        }
        public class ChatItem
        {
            public string Sender { get; set; } // "user" hoặc "bot"
            public string Text { get; set; }
        }
}

