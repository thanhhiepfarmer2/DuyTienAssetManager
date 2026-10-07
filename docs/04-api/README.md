# API

Web và app Android/iOS dùng chung backend/API. TASK-003 đã có
`GET /api/system/status` chỉ để kiểm tra scaffold/readiness và không trả secret
hoặc resource ID. Các route nghiệp vụ trong [API-SCOPE](API-SCOPE.md) vẫn là
outline `PROPOSED`, chưa phải OpenAPI hay feature đã triển khai.

Dùng OpenAPI nếu phù hợp; chọn một nguồn contract authoritative khi có code,
không giữ spec thủ công mâu thuẫn với implementation.
