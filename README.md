# 🎟️ HTicket - Hệ Thống Đặt Vé Thông Minh Tích Hợp Trợ Lý Ảo AI

HTicket là một hệ thống quản lý và phân phối vé sự kiện tiên tiến dựa trên kiến trúc **.NET 10 MVC**. Hệ thống được nâng cấp đột phá bằng cách tích hợp trợ lý ảo AI thông minh, kết hợp giữa công nghệ tìm kiếm ngữ nghĩa (Vector Retrieval) và mô hình ngôn ngữ lớn (LLM) giúp tự động hóa quy trình tư vấn và xử lý nghiệp vụ đặt/hủy vé theo thời gian thực.

---
Live demo : hticket-hung.up.railway.app
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

## 🚀 Hướng Dẫn Cài Đặt & Khởi Chạy Cục Bộ

Dự án hỗ trợ khởi chạy trực tiếp bằng **.NET 10 SDK** (Khuyên dùng trong quá trình phát triển và kiểm thử).

### 1. Chuẩn bị môi trường
* Máy tính đã cài đặt **.NET SDK 10.0** trở lên.
* Cơ sở dữ liệu đã được cấu hình sẵn (SQL Server / PostgreSQL).

### 2. Cấu hình ứng dụng (Local appsettings)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=HTicketDb;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "GeminiConfig": {
    "Url": "https://openrouter.ai/api/v1/chat/completions",
    "ApiKey": "KEY OPENROUTER"
  }
}

3. Khởi chạy dự án
Mở Terminal tại thư mục gốc của dự án (nơi chứa file HTicket.csproj và Program.cs) và chạy các lệnh sau:

Bash
# Phục hồi các gói thư viện (NuGet Packages)
dotnet restore

# Biên dịch (Build) dự án kiểm tra lỗi
dotnet build

# Khởi chạy dự án ở chế độ Watch (Tự động cập nhật khi sửa code/Prompt)
dotnet watch run
🎯 Kịch Bản Thử Nghiệm Luồng AI (Demo & Testing Workflow)
Khi ứng dụng chạy, luồng xử lý câu hỏi từ giao diện Chatbot sẽ đi qua 3 bước thực tế theo logic code:

Giai đoạn 1 - Sàng lọc Vector (RAG): Người dùng nhập câu hỏi (Ví dụ: "Tôi muốn mua vé xem Rap Việt"). Hệ thống gửi tập sự kiện thô sang Python AI để tìm các Event liên quan bằng thuật toán Vector Embedding.

Giai đoạn 2 - Kiểm tra Kho vé & Đơn hàng: Hệ thống tự động bốc danh sách vé còn lại (RemainingQuantity) của các sự kiện đã lọc, đồng thời tra cứu lịch sử đơn hàng của khách theo customerId để làm ngữ cảnh phụ trợ.

Giai đoạn 3 - Phân tích & Thực thi lệnh ẩn: Dữ liệu được nạp vào Prompt gửi cho Gemini để trả ra nội dung tư vấn dạng HTML (thẻ <b> in đậm tên show, <br> xuống dòng). Nếu khách hàng chốt mua hoặc hủy vé hợp lệ, AI sẽ tự động sinh lệnh ẩn dạng [CREATE_ORDER|MãVé|SốLượng] hoặc [CANCEL_ORDER|MãĐơn] để hệ thống phía sau xử lý tiếp.

Các câu hỏi Test khi Demo:
Test Tra cứu & Gợi ý: Hỏi "Ngày 05/12/2026 có show gì không?" -> AI sẽ nhận diện show Golf là Sắp diễn ra, từ chối đặt vé và tự động gợi ý thêm show Rap Việt (Đang mở bán).

Test Chặn lỗi Kho vé: Đặt số lượng vé lớn hơn số lượng tồn kho thực tế -> AI sẽ đọc data từ tickets và từ chối: "Hiện tại loại vé này chỉ còn X vé".

Test Bảo mật: Hệ thống chặn hoàn toàn việc hiển thị Mã vé công khai ra màn hình chat và giấu kín mọi thông báo Exception.
🌐 Hướng Dẫn Triển Khai Lên Production (Railway Deployment)Dự án được cấu hình tối ưu hóa để deploy trực tiếp lên nền tảng Railway thông qua cấu hình Nixpacks (nixpacks.toml), tự động nhận diện môi trường .NET 10 để build source code mà không cần cấu hình Docker thủ công.
Các bước thiết lập trên Railway:Kết nối Repo: Lên dashboard của Railway, chọn New Project  Deploy from GitHub repository và chọn repo HTicket.
Cấu hình Biến môi trường (Variables):
Vào tab Variables trên Railway và thêm các cấu hình bảo mật sau:
GeminiConfig__ApiKey = KEY OPENROUTER
GeminiConfig__Url = https://openrouter.ai/api/v1/chat/completions
ConnectionStrings__DefaultConnection = Chuỗi_kết_nối_database_productionDeploy:
ASPNETCORE_ENVIRONMENT = Development
Railway sẽ tự động khởi tạo môi trường .NET 10, chạy dự án và cấp đường link public chính thức cho chatbot.
