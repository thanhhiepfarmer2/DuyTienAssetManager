# TEST-REPORT — TASK-012

Ngày2026-10-13, Asia/Saigon. Working directory `D:\Dev\QuanLyTaiSan_DuyTien`. Nguồn: **AI chạy/quan sát trực tiếp** cho build/tests/probes dưới đây. Không có người dùng chạy/xác nhận thủ công, Android runtime, browser hoặc Google live mới. TASK-012 **IN_REVIEW**, hoàn tất phần local, không DONE.

## Phạm vi / môi trường

Tiếp tục project MAUI hiện có, .NET10; Android login/đổi mật khẩu/SecureStorage/rotation/logout, Asset list/search/filter/page/detail/create/edit DRAFT_DEMO, camera/pick/preview/upload/retry/reconcile. Không ứng dụng mới, iOS TASK-013, offline DB, SQL/SQLite, rule công ty mới hoặc Google credential trong app. Backend/action scopes/single-writer/metadata guard không sửa.

Ngoại lệ người dùng cho bắt đầu dù TASK-005/007/009 IN_REVIEW; TASK-003 IN_REVIEW, TASK-004 DONE, TASK-005–011 IN_REVIEW/gates/DEFERRED_BY_USER/STOPPED_BY_USER giữ. Đầu lượt56file TASK-010/011 chưa commit, `main`, HEAD `463896d82629847ebd41866eb3e0cb97becfc2f2`; không stage/commit/push/deploy/real writes. Fixtures GIẢ trong RAM, local Kestrel dùng fake auth/business/content stores, không backend Google dev hiện có.

SDK `10.0.400`; workload set `10.0.400-manifests.b0ae88bd`, installed `maui 10.0.20/10.0.100`. JDK Temurin `21.0.12.1+1`, SDK Android `android-36`, build-tools `36.0.0`. Target `net10.0-android`, generated manifest minSdk21/targetSdk36, cleartext disabled, backup disabled, camera optional; storage permissions chỉ đếnAPI32. Không READ_MEDIA_* toàn thư viện trên Android mới. Đây là cấu hình compile, không permission/device pass.

## Expected / actual

| Case | Expected | Actual / nguồn |
|---|---|---|
| Login/restore | Token pair trong secret-store, bound backend; không lưu password/role snapshot | PASS_LOCAL fake secret-store; production SecureStorage adapter compile, **NOT_RUN_OS_SECURESTORAGE** |
| Rotation | Một rotation cho access hết hạn; marker trước send; crash/lost response/storage fail không reuse cũ | PASS_LOCAL client/fake HTTP; existing Auth API fake-time1h rotation/refresh thực của service; không thời gian thực/Google |
| Revoke/logout | 401 refresh một lần, thất bại xóa pair; user lock revoke; logout offline vẫn ngừng phiên RAM/xóa store nếu store hoạt động | PASS_LOCAL fake store + backend Auth API; UI không khẳng định server logout thành công khi mạng lỗi |
| Password | Bắt đổi trước business, sai current giữ phiên; min8 không complexity; đổi thành công đăng nhập lại | PASS_LOCAL workflow + existing Auth API, mật khẩu thử giả không ghi report; policy server không sửa |
| Browse | Search/filter/page/total/asOf/detail qua server, Viewer thấy đúng nhánh và không hidden total/search/file | PASS_LOCAL client-to-Kestrel; filters q/category/descendant/org/location/condition/custodian, page20/max100; không load toàn catalog |
| Form | Cùng AssetInput/ValidateInput; ID/code/state do backend, DRAFT_DEMO;422 giữ nội dung,409 không overwrite | PASS_LOCAL actual API create/update/stale/explicit rebase; local choices chỉ DRAFT và refs được cấp; source/rule công ty OPEN/TBD |
| Uncertain write | Giữ DTO/version/tree/key, không sửa payload/new key khi chưa xác nhận | PASS_LOCAL drop response sau server commit → retry Replayed, chỉ1Asset; sửa payload bị chặn; không thêm mã/counter client |
| Photo selection | Cancel/permission deny giữ ảnh/mô tả, không upload; PNG/JPEG/cap10MiB, stream bounded | PASS_LOCAL fake picker/workflow/concurrent selection; adapter MediaPicker/FilePicker compile. **NOT_RUN_CAMERA/PERMISSION_OS** |
| Upload | Multipart đúng file/expectedVersion/description/isPrimary/key; pending không success; replay/reconcile một file | PASS_LOCAL actual Media API/fake content: lost response/retry1file; FAILED→reconcileAVAILABLE;409 owner version explicit reload; nonAVAILABLE không báo thành công |
| Gateway | Bearer trong header, scoped preview bounded; không token URL/Google URL/Drive ID từ app | PASS_LOCAL actual gateway với ảnh GIẢ và hidden file404; HTTP production redirects/cookies disabled, HTTPS only; **NOT_RUN_DEVICE_TLS** |
| UI | Login/password/list/detail/form/photos có busy/empty/error/conflict/retry/state và giữ fields | MAUI code/XAML compile + core workflow/API tests; **NOT_RUN_ANDROID_UI** cho navigation/layout/keyboard/buttons/camera UI. Không coi compile/test core là tương tác thiết bị PASS |

Ngưỡng thời gian HTTP client: headers45s, toàn thao tác/body60s (network timeout, không chính sách session). Session policies server nguyên vẹn. Request/rotation/logout tuần tự; response sai contract/over cap không thành success. Không cache dữ liệu offline; draft/photo chỉ RAM. Đang uncertain thì form giữ nguyên/key và chặn đóng trang vô ý; có logout bỏ RAM. App bị OS đóng hoặc logout mất bản RAM, không tự gửi upload mới; backend giữ PENDING/FAILED, có đối soát theo fileId đã trả/danh sách ảnh. Không tự merge409 hoặc phê duyệt DRAFT. Scope client UI chỉ hỗ trợ trải nghiệm, enforcement nằm ở server.

## Lệnh / exit thực tế

Lệnh chính và exact include paths tại [COMMANDS](../COMMANDS.md). Build/test/format tuần tự, chờ exit trước lệnh dùng chung output kế tiếp.

- `dotnet --version`: exit0 →10.0.400; `dotnet workload list`: exit0 →versions trên.
- `dotnet restore src/DuyTien.AssetManagement.Mobile/DuyTien.AssetManagement.Mobile.csproj --nologo`: exit0. Restore project nguyên bản có hai target hiện hữu; không build/chạy iOS, không TASK-013.
- Hai thử restore trước đó dùng `-p:TargetFramework=net10.0-android` và `-p:TargetFrameworks=net10.0-android`: mỗi restore exit0 nhưng hai build kế tiếp exit1 NETSDK1005 (Application assets thiếu net10.0). Sửa bằng restore chuẩn; không đổi SDK/framework của thư viện.
- `dotnet build src/DuyTien.AssetManagement.Mobile/DuyTien.AssetManagement.Mobile.csproj -f net10.0-android --no-restore --nologo -v:minimal`: sau sửa restore exit0,0warning/error,1m32.75s; sau self-review21.20s và13.24s, đều exit0; sau sửa return-to-list/detail/cookies cuối **exit0,0warning/error,9.15s**. Có Debug APK trong bin (ignored), không cài/phân phối.
- `dotnet test tests/DuyTien.AssetManagement.ArchitectureTests/DuyTien.AssetManagement.ArchitectureTests.csproj --no-restore --nologo -v:minimal --filter "FullyQualifiedName~Mobile"`: đầu21total/20pass/1fail/0skip exit1 (test đọc marker bằng JSON options không đúng),1warning test handler tên Send. Sửa options giống client/đổi tên Respond. Lần thêm contract compile exit1 CS1061 (test gọi sai tên service khóa user); chưa chạy test trong lần đó. Sửa tên service đúng, không đổi server hoặc expected để pass. Tiếp30/30 exit0,38/38 exit0, cuối **42/42pass,0failed/0skipped,exit0,3s**, build test không warning/error.
- 42test cuối:23session cases,10client-to-backend contract cases,9photo workflow cases. Link chính Mobile/Client source vào test net10.0, không sao chép implementation. Core không sửa sau lượt42; chỉ MAUI refresh-on-return/HTTP handler UI chỉnh rồi build Android lại.
- `dotnet format whitespace` Mobile exact16C# paths, tests exact3files, rồi Mobile3UI/runtime paths: exit0 mỗi lệnh, không format nguồn/tests TASK-010/011.
- `java -version` bằng JDK đường dẫn cấu hình: exit0,version trên. `adb devices -l` bằng SDK platform-tools: exit0, danh sách rỗng; không có bằng chứng thiết bị/emulator cài/chạy.
- `git diff --check`: exit0 đã quan sát trước đồng bộ report; kiểm tra sau cập nhật docs ghi ở evidence cuối. Không rerun full TASK-005–011 test/UAT hoặc Google live.

## Self-review / lỗi và giới hạn

Đọc và áp dụng `.NET backend patterns` cho async/DI/DTO/tests, `interaction-design` cho busy/error/empty/feedback và `ai-debt-detector` cho failure/resource/edge review. Không subagent/independent review hoặc audit bảo mật toàn diện. Không áp dụng SQL/cache/React từ ví dụ skill. Bổ sung rotation marker/storage-failure cleanup, request/body cap/deadline, photo payload bất biến, concurrent picker guard, response owner/DRAFT guard, explicit409 rebase choices, no-cookie/no-redirect, scoped reload khi quay lại. HttpClient test host/streams/CTS/requests/responses dispose; production HttpClient giữ theo runtime, thay khi đổi endpoint sau logout.

Không giữ request credential/token/error body vào report/log; token envelope ToString redacted, app không store password. Không đọc user-secrets/Google credentials hoặc yêu cầu secret mới. Không tạo runner/UAT credential/file tạm bằng task này. Secret-store xóa đã chứng minh với fake adapter, chưa với OS thực. Camera/framework có thể quản lý file cache bên trong; app không tạo kho ảnh/offline hay copy ảnh xuống disk. Mất process giữ operation backend nhưng không phục hồi payload RAM — giới hạn đã ghi, không tự nhận crash-resume upload local.

## Gate còn mở

- **NOT_RUN_ANDROID_RUNTIME / UI**: cài/chạy APK trên Android ghi device/OS; navigation/back/loading/form/keyboard/layout; camera ảnh thật, cancel/deny OS, picker/library URI, SecureStorage restore/delete/backup behavior, thực mạng/retry/phiên revoke trên thiết bị. adb không thiết bị; không yêu cầu người dùng cung cấp ngay.
- **NOT_RUN_DEVICE_TLS / GOOGLE**: endpoint môi trường được thiết bị tin cậy, Sheets/Drive/ACL/drive.file/file metadata thật, quota/latency/restart. Backend URL/company host vẫn TBD; không insecure certificate workaround.
- **OPEN/TBD nguồn/rule**: dependency workbook/OrgLocation/code/required business fields chưa xác minh; demo fixture không thành dữ liệu công ty. Guard metadata vẫn fail-closed khi registry mới chưa đối soát; Android không có tree mutation.
- **NOT_RUN_IOS**: không build/cài/chạy hoặc sửa platform iOS TASK-013. Android local không chứng minh iOS.

Bước tiếp theo thuộc TASK-012 là các gate runtime/device/Google khi có phạm vi cho phép; không giao UAT thủ công mới hoặc tự triển khai TASK-013. Giữ IN_REVIEW.

## Đối chiếu cuối — AI chạy/quan sát trực tiếp

Checker UTF8/link/JSON/status chạy exit0:85modified/untracked paths,125local links,0missing-link/artifact-or-status issue. Các subprocess git diff--check/index-empty/guard/auth/iOS/activeScript-unchanged/APK-ignored đều exit0; HEAD main463896d giữ. Không scan values của user-secrets; file-path checks không là audit bảo mật.

So baseline56paths đầu lượt:48fingerprint nguyên vẹn (gồm TASK-010/011 Task/Test Report/evidence/source/tests và guard CategoryCatalogService),8shared docs được bổ sung trạng thái/ngoại lệ TASK-012,0change ngoài allowlist. Không stage/commit/push; các gate/task trước không đổi. Diff check sau ghi bằng chứng được chạy tiếp trước kết thúc; không build/test lại vì chỉ doc edit.
