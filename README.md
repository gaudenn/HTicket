# 🎟️ HTicket - Hệ Thống Đặt Vé Thông Minh Tích Hợp Trợ Lý Ảo AI

HTicket là một hệ thống quản lý và phân phối vé sự kiện tiên tiến dựa trên kiến trúc **.NET 10 MVC**. Hệ thống được nâng cấp đột phá bằng cách tích hợp trợ lý ảo AI thông minh, kết hợp giữa công nghệ tìm kiếm ngữ nghĩa (Vector Retrieval) và mô hình ngôn ngữ lớn (LLM) giúp tự động hóa quy trình tư vấn và xử lý nghiệp vụ đặt/hủy vé theo thời gian thực.

---
Live demo : hticket-hung01842.up.railway.app
## ✨ Tính Năng Nổi Bật

- **🤖 Trợ Lý Ảo H-TICKET:** Sử dụng **Gemini API** để giao tiếp, hiểu và phản hồi khách hàng bằng ngôn ngữ tự nhiên, mang lại trải nghiệm tư vấn cá nhân hóa, chi tiết và lịch sự.
- **🔍 Tìm Kiếm Ngữ Nghĩa (RAG - Vector Search):** Tích hợp với dịch vụ Vector AI (`hticketw2v`) để sàng lọc nhanh các sự kiện liên quan dựa trên ngữ cảnh câu hỏi của khách, vượt trội hơn hẳn so với cách tìm kiếm từ khóa (Keyword Matching) truyền thống.
- **⚙️ Thực Thi Lệnh Nghiệp Vụ Tự Động (Function Calling):** AI không chỉ trả lời suông mà có khả năng tự phân tích kho vé thực tế, kiểm tra số lượng tồn kho để sinh các cấu trúc lệnh điều hướng hệ thống chính xác:
  - Đặt vé: `[CREATE_ORDER|MãVé|SốLượng]`
  - Hủy đơn: `[CANCEL_ORDER|MãĐơn]`
- **📊 Quản Lý Trạng Thái Kinh Doanh:** Tự động nhận diện và chặn các yêu cầu đặt vé đối với sự kiện *"Sắp diễn ra"*, đồng thời chủ động gợi ý từ 1-2 sự kiện *"Đang mở bán"* để tối ưu chuyển đổi doanh thu.
- **🛡️ Kiến Trúc Bảo Mật Đa Tầng:** - Validate dữ liệu đầu vào nghiêm ngặt chống các lỗ hổng Injection.
  - Áp dụng cơ chế cô lập lỗi độc lập (Exception Isolation). Toàn bộ log lỗi hệ thống (Database, API lỗi...) được giấu kín ở phía Backend để tránh lỗ hổng rò rỉ thông tin hệ thống (**Information Leakage**). Khách hàng hoặc Hacker chỉ nhìn thấy thông báo chung an toàn.

---

## 🛠️ Công Nghệ Sử Dụng

- **Backend:** .NET 10 (ASP.NET Core MVC)
- **Database Framework:** Entity Framework Core cho .NET 10 (LINQ, AsNoTracking tối ưu hiệu năng tối đa)
- **AI & Embedding:** Google Gemini Pro API, Python Vector Retrieval Service (Word2Vec/Embedding API)
- **DevOps/Deployment:** Nixpacks (`nixpacks.toml`), Docker (Dockerfile)

---

## 🚀 Ảnh demo
1.
<img width="1917" height="996" alt="{555510B4-02CE-4728-B25C-22E641A1B183}" src="https://github.com/user-attachments/assets/a96169a8-783d-4b01-bf58-6cf77598e733" />
-- Giao diện quản lý dự đoán tốc độ bán vé, có đầy đủ tên sự kiện hạng vé, số vé bán được trong ngày, dự đoán số ngày hết vé, và lời khuyên của AI.

2.
<img width="1918" height="1042" alt="{E573D581-2D35-428F-A9A2-45C1A804EABC}" src="https://github.com/user-attachments/assets/34973321-343a-4ac8-b2fd-5d53c6a1e68c" />
<img width="1919" height="1037" alt="{3736B3FC-2F58-41D4-AF1F-ED432A2D4A62}" src="https://github.com/user-attachments/assets/01fdbee7-5933-4687-aade-675a58fc8952" />
<img width="1920" height="1046" alt="{782284D1-8AD8-4836-8870-625788935D30}" src="https://github.com/user-attachments/assets/3da2051e-160f-49f9-87d3-489e44ab6cb3" />
<img width="1917" height="1041" alt="{A4F88DC9-B64F-4C29-A557-B0D77F989095}" src="https://github.com/user-attachments/assets/a048f083-6e61-4364-8ebb-9f7c2ed628a1" />

-- Hội thoại chat giữa người dùng và chatbot có quy trình tư vấn hỗ trợ đặt vé và hủy vé theo yêu cầu.







