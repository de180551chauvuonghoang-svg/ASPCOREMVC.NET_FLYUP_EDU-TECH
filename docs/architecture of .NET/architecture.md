# 🏗️ Kiến trúc Hệ thống trong .NET — Toàn cảnh

> **Mục tiêu**: Hiểu rõ các kiến trúc phổ biến trong .NET, khi nào dùng cái nào, và tại sao EduHub chọn Clean Architecture.

---

## Tổng quan — 5 Kiến trúc Chính

```
┌─────────────────────────────────────────────────────────┐
│                  ĐỘ PHỨC TẠP TĂNG DẦN                  │
│                                                         │
│  Monolith  →  Layered  →  Clean  →  Modular  →  Micro  │
│                                                         │
│  Nhỏ/Đơn           Trung bình            Lớn/Enterprise │
└─────────────────────────────────────────────────────────┘
```

---

## 1️⃣ Monolithic Architecture (Kiến trúc Nguyên khối)

```
┌──────────────────────────────────────────┐
│              ONE BIG APP                 │
│  UI + Business Logic + Data Access       │
│  tất cả trong 1 project duy nhất         │
└──────────────────────────────────────────┘
          │
          ▼
    1 Database duy nhất
```

### Đặc điểm
- Toàn bộ ứng dụng chạy như **1 process** duy nhất
- Deploy 1 lần là xong
- Code dễ lẫn lộn nếu không có kỷ luật

### Cấu trúc điển hình trong .NET
```
MyApp/
├── Controllers/
├── Models/
├── Views/
├── Services/
└── Data/   ← DbContext nằm đây luôn
```

### Khi nào dùng

| ✅ Nên dùng | ❌ Không nên dùng |
|---|---|
| Startup nhỏ, team 1–3 người | App lớn 20+ dev cùng làm |
| MVP, prototype cần ra nhanh | Cần scale từng phần riêng lẻ |
| Deadline gấp | Yêu cầu high availability |
| CRUD app đơn giản | Nhiều domain nghiệp vụ phức tạp |

---

## 2️⃣ N-Layered Architecture (Kiến trúc Phân tầng)

```
┌─────────────────────────┐
│   Presentation Layer    │  ← Controllers, Views, API
├─────────────────────────┤
│   Business Logic Layer  │  ← Services, Validators
├─────────────────────────┤
│   Data Access Layer     │  ← Repositories, DbContext
├─────────────────────────┤
│       Database          │  ← SQL Server / PostgreSQL
└─────────────────────────┘
```

### Đặc điểm
- Phân tách rõ ràng theo **kỹ thuật** (UI, logic, data)
- Layer trên gọi layer dưới, KHÔNG được gọi ngược
- **Phổ biến nhất** trong các công ty Việt Nam hiện tại

### Cấu trúc điển hình trong .NET
```
EduHub.Presentation   ← ASP.NET Core (Controllers, Views)
EduHub.Services       ← Business Logic (CourseService, UserService)
EduHub.Repository     ← Data Access (ICourseRepository, EF Core)
EduHub.Models         ← Entities (Course, User, Enrollment)
```

### Khi nào dùng

| ✅ Nên dùng | ❌ Hạn chế |
|---|---|
| Team 3–10 người | Domain phức tạp (nhiều business rules) |
| CRUD app có nghiệp vụ trung bình | Khi cần unit test coverage cao |
| Đã quen với pattern này | Business logic dễ "lọt" vào layer sai |
| Cần tốc độ phát triển nhanh | Tầng Presentation dễ phụ thuộc trực tiếp vào DB |

---

## 3️⃣ Clean Architecture ← *Dùng trong EduHub*

```
            ┌─────────────────────┐
            │       Domain        │  ← Trung tâm, không phụ thuộc gì
            │  Entities, Enums    │
            │  Interfaces         │
            └──────────┬──────────┘
                       │ ▲ (chỉ biết Interface)
            ┌──────────▼──────────┐
            │     Application     │  ← Business Logic thuần túy
            │  Commands, Queries  │
            │  DTOs, Services     │
            └──────────┬──────────┘
                       │ ▲ (implement interface)
       ┌───────────────▼──────────────────────┐
       │   Infrastructure      │     Web       │
       │  EF Core, SQL Server  │  Controllers  │
       │  Email, File Storage  │  Razor Pages  │
       │  (implement interfaces)│  Blazor       │
       └───────────────────────────────────────┘
```

### Luật bất di bất dịch — Dependency Rule
> ⚠️ **Dependencies chỉ trỏ vào trong (hướng về Domain)**
> - `Domain` không biết gì về EF Core, HTTP, SQL
> - `Application` không biết cụ thể dùng SQL Server hay PostgreSQL
> - `Infrastructure` implement các interface mà `Application` định nghĩa
> - `Web` chỉ là cổng vào, không chứa business logic

### Cấu trúc trong EduHub
```
src/
├── EduHub.Domain/          # Entities, Enums, Interfaces
├── EduHub.Application/     # CQRS, Commands, Queries, DTOs
├── EduHub.Infrastructure/  # EF Core, Email, FileUpload
└── EduHub.Web/             # MVC, Razor Pages, Blazor, API

tests/
└── EduHub.UnitTests/       # Test Application layer độc lập
```

### Project References (luồng phụ thuộc)
```
Domain          ←  không phụ thuộc ai
Application     →  Domain
Infrastructure  →  Application → Domain
Web             →  Application + Infrastructure
UnitTests       →  Application (test độc lập với DB)
```

### Khi nào dùng

| ✅ Nên dùng | ❌ Overkill khi |
|---|---|
| Team 5–15 người | App CRUD quá đơn giản |
| Business logic phức tạp | Deadline cực kỳ gấp |
| Cần unit test coverage cao | Team chưa có kinh nghiệm về kiến trúc |
| App cần maintain lâu dài (2–5 năm) | Startup nhỏ chưa có user |

---

## 4️⃣ Modular Monolith Architecture

```
┌─────────────────────────────────────────────────┐
│                    ONE APP                      │
│                                                 │
│  ┌────────────┐  ┌────────────┐  ┌───────────┐  │
│  │   Module   │  │   Module   │  │  Module   │  │
│  │  Courses   │  │   Users    │  │  Payment  │  │
│  │            │  │            │  │           │  │
│  │  Domain    │  │  Domain    │  │  Domain   │  │
│  │  App       │  │  App       │  │  App      │  │
│  │  Infra     │  │  Infra     │  │  Infra    │  │
│  └─────┬──────┘  └─────┬──────┘  └─────┬─────┘  │
│        └───────────────┴───────────────┘        │
│                  Shared Kernel / Event Bus       │
└────────────────────────┬────────────────────────┘
                         │
                  Shared Database
```

### Đặc điểm
- Vẫn là 1 app, 1 process — nhưng **chia module rõ ràng theo domain**
- Modules giao tiếp qua **Interface hoặc Domain Events**, KHÔNG gọi nhau trực tiếp
- Bước đệm hoàn hảo trước khi chuyển sang Microservices
- **Đang rất phổ biến năm 2024–2025**

### Khi nào dùng

| ✅ Nên dùng | ❌ |
|---|---|
| Team 10–30 người | Team nhỏ — overhead lớn không cần thiết |
| Nhiều domain nghiệp vụ rõ ràng | App không có nhiều domain riêng biệt |
| Muốn migrate lên Microservices dễ sau này | |
| Muốn các team làm việc độc lập | |

---

## 5️⃣ Microservices Architecture

```
              ┌─────────────────────────┐
              │       API Gateway        │  ← Nginx / YARP / Ocelot
              └──┬──────────┬───────────┘
                 │          │
       ┌─────────▼──┐  ┌────▼────┐  ┌──────────────┐
       │  Course    │  │  User   │  │   Payment    │
       │  Service   │  │ Service │  │   Service    │
       │  Own DB    │  │ Own DB  │  │   Own DB     │
       └─────┬──────┘  └────┬────┘  └──────┬───────┘
             │              │               │
       ┌─────▼──────────────▼───────────────▼──────┐
       │         Message Bus (RabbitMQ / Kafka)      │
       └─────────────────────────────────────────────┘
```

### Đặc điểm
- Mỗi service = 1 app riêng, 1 database riêng, 1 team riêng
- Giao tiếp qua **HTTP / gRPC** hoặc **Message Queue**
- Scale từng service độc lập
- Cực kỳ phức tạp khi debug, monitor, deploy

### Khi nào dùng

| ✅ Nên dùng | ❌ TUYỆT ĐỐI TRÁNH khi |
|---|---|
| Team 50+ người, nhiều team riêng biệt | Team dưới 20 người |
| App cần scale từng phần độc lập | Chưa có đội DevOps / Kubernetes |
| Traffic cực cao (Tiktok, Shopee level) | Business domain chưa ổn định |
| Đã có hạ tầng DevOps mạnh | Muốn dùng cho "nghe có vẻ xịn" |

---

## 📊 So sánh tổng hợp

| Tiêu chí | Monolith | N-Layered | Clean Arch | Modular | Microservices |
|---|:---:|:---:|:---:|:---:|:---:|
| **Độ phức tạp** | 🟢 Thấp | 🟡 TB | 🟠 Cao | 🟠 Cao | 🔴 Rất cao |
| **Team size phù hợp** | 1–3 | 3–10 | 5–15 | 10–30 | 50+ |
| **Testability** | ⭐ | ⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐ |
| **Scalability** | ⭐ | ⭐⭐ | ⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Tốc độ dev ban đầu** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐ | ⭐ |
| **Maintainability** | ⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ |
| **Phổ biến tại VN** | 🟡 | 🟢 Nhiều nhất | 🟡 Tăng dần | 🟠 Mới nổi | 🔴 Ít |

---

## 🎯 Lộ trình thực tế của .NET Developer Việt Nam

```
🟢 Junior (0–2 năm)
   ├─ N-Layered Architecture (gặp nhiều nhất)
   └─ Monolith MVC đơn giản

🟡 Mid-level (2–4 năm)
   ├─ Clean Architecture
   ├─ CQRS + MediatR
   └─ Repository Pattern chuyên nghiệp

🟠 Senior / Tech Lead (4+ năm)
   ├─ Modular Monolith
   ├─ Microservices (event-driven)
   └─ DDD (Domain-Driven Design)
```

---

## 💡 Tại sao EduHub dùng Clean Architecture?

1. **Học đúng cách ngay từ đầu** — Tránh thói quen xấu của N-Layered thiếu kỷ luật
2. **Cover nhiều kiến thức nhất** — DI, CQRS, Repository, UoW, Middleware đều có chỗ rõ ràng
3. **Testable** — Có thể unit test business logic mà không cần kết nối database
4. **Chuẩn Enterprise** — Kiến trúc phổ biến nhất ở các công ty .NET chuyên nghiệp
5. **Nền tảng vững** — Hiểu Clean Arch → chuyển sang Modular/Microservices rất tự nhiên

---

## 📚 Tài liệu tham khảo

- [Clean Architecture - Robert C. Martin (Uncle Bob)](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [Microsoft - Common web application architectures](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures)
- [Modular Monolith - Milan Jovanović](https://www.milanjovanovic.tech/blog/what-is-a-modular-monolith)
