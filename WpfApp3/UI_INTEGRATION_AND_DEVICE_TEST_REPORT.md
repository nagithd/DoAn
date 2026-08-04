# BÁO CÁO CHỈNH SỬA GIAO DIỆN, KIỂM TRA PHẦN MỀM VÀ CÁC HẠNG MỤC CHỜ THIẾT BỊ

**Dự án:** Battery Inspection System – Industrial HMI  
**Ứng dụng:** WPF .NET 8  
**Ngày kiểm tra:** 30/07/2026  
**Thư mục dự án:** `D:\capstone\WPF\WpfApp3`

---

## 1. Mục đích báo cáo

Báo cáo này tổng hợp các chỉnh sửa mới nhất trên giao diện WPF, kết quả
kiểm tra có thể thực hiện ngay trên máy phát triển và danh sách các chức
năng chỉ có thể xác nhận sau khi kết nối thiết bị thật.

Kiến trúc được sử dụng sau khi thống nhất gồm hai camera có nhiệm vụ khác
nhau:

1. Camera IMITECH kết nối với máy Windows cung cấp ảnh đầu vào cho mô hình
   AI. Mô hình AI sẽ phân loại vật và chọn chu trình robot đã được lập
   trình trước.
2. Camera trên DOFBOT kết nối với Jetson quan sát đầu băng tải. Camera này
   xác định vật đã đi vào vùng trigger, đo góc tương đối của vật, tính góc
   Servo 5 và xác định thời điểm bắt đầu chu trình.
3. Robot API trên Jetson nhận chu trình đã chọn và trigger từ camera
   DOFBOT để thực hiện thao tác gắp.
4. Phần điều khiển động cơ băng tải chưa có giao thức phần cứng chính
   thức nên vẫn được giữ dưới dạng placeholder minh bạch.

---

## 2. Kết quả tổng quát

| Hạng mục | Trạng thái |
|---|---|
| Build Debug | Thành công, 0 lỗi, 0 cảnh báo |
| Build Release | Thành công, 0 lỗi, 0 cảnh báo |
| XAML mới | Biên dịch thành công |
| Dữ liệu AI PCB giả | Đã loại bỏ |
| Nút Execute Model/Stop Model giả | Đã loại bỏ |
| Tab băng tải | Đã chuyển thành placeholder đúng mục đích |
| Camera Windows | Đã sửa trạng thái kết nối và thông tin feed |
| Giao diện Vision Trigger | Đã bổ sung |
| Robot status response | Đã hỗ trợ cả cấu trúc cũ và mới |
| System Log | Đã liên kết với camera, robot và vision |
| Kiểm tra Robot API trên Jetson | Chưa thực hiện được; kết nối bị timeout |
| Kiểm tra camera và chuyển động thật | Chờ kết nối thiết bị |

Tại thời điểm lập báo cáo, yêu cầu đọc:

`GET http://192.168.137.179:7000/health`

không nhận được phản hồi trong thời gian 3 giây. Kết quả này chỉ cho biết
Jetson/Robot API không truy cập được từ máy hiện tại tại thời điểm kiểm
tra. Nó không kết luận rằng mã Robot API bị lỗi.

---

## 3. Các phần đã chỉnh sửa

### 3.1. Tách rõ vai trò hai camera

Tab camera ban đầu đã được đổi thành `AI Camera`. Giao diện ghi rõ đây là
camera IMITECH trên Windows, dùng để cung cấp ảnh cho mô hình AI sau này.

Các thông tin cố định `1920×1080 @ 30fps` đã bị loại bỏ. Resolution và
FPS hiện lấy từ camera service sau khi stream được mở thành công.

Một tab mới có tên `DOFBOT Trigger` đã được thêm vào. Tab này dành riêng
cho camera DOFBOT trên Jetson và không sử dụng chung với camera IMITECH.

Sự tách biệt này phù hợp với luồng:

```text
IMITECH trên Windows
        ↓
Mô hình AI phân loại
        ↓
Chọn class/chu trình robot
        ↓
Camera DOFBOT phát hiện vật đi vào vùng trigger
        ↓
Tính Servo 5 và thời gian chờ
        ↓
Robot API thực hiện chu trình đã chọn
```

### 3.2. Sửa trạng thái kết nối camera Windows

Trước đây `CameraViewModel` luôn gán trạng thái `Connected` sau khi gọi
`StartCamera()`, kể cả khi OpenCV không mở được stream.

Camera service hiện trả về giá trị thành công hoặc thất bại. Giao diện
chỉ chuyển sang `Connected` nếu:

- camera được mở thành công;
- service chuyển sang trạng thái đang chạy;
- luồng capture đã được khởi tạo.

Nếu MVS vẫn capture được nhưng OpenCV không mở được thiết bị, giao diện
sẽ báo lỗi thay vì báo kết nối giả.

Camera timer và camera service cũng được giải phóng khi cửa sổ chính đóng,
giảm nguy cơ camera vẫn bị giữ sau khi thoát ứng dụng.

### 3.3. Thêm giao diện DOFBOT Vision Trigger

Giao diện mới hỗ trợ các thao tác:

- nhập địa chỉ Jetson API;
- kết nối/ngắt kết nối;
- đọc `/vision/status`;
- mở camera bằng `/vision/start`;
- dừng camera bằng `/vision/stop`;
- reset trigger bằng `/vision/reset-trigger`;
- hiển thị ảnh debug từ `/vision/frame.jpg`;
- đưa robot về `VISION_HOME`;
- xem trạng thái camera, robot-ready, auto-trigger và trigger latch;
- xem số frame đã xử lý và số frame phát hiện ổn định;
- xem vị trí vật, góc tương đối và góc Servo 5;
- xem thời gian vật đến điểm gắp và `start_delay_ms`;
- xem job gần nhất được tạo bởi vision.

Các thông số timing có thể cấu hình trên giao diện:

- tốc độ băng tải, đơn vị mm/s;
- khoảng cách từ vùng camera đến điểm gắp, đơn vị mm;
- thời gian robot đi đến vị trí kẹp, đơn vị ms;
- processing margin, đơn vị ms.

`auto_trigger` không được tự động bật khi mở giao diện. Người vận hành
phải đánh dấu `Enable automatic robot trigger` và nhấn
`Apply timing and mode`.

### 3.4. Chuẩn bị kết nối kết quả AI với chu trình robot

Vision Trigger có trường `Selected AI class / robot cycle`. Trong giai
đoạn chưa có model, người vận hành có thể chọn thủ công:

- `plastic`;
- `metal`;
- `paper`.

Giá trị được gửi đến trường `default_class` của `/vision/config`. Khi
model AI hoàn thành, phần kết nối AI chỉ cần cập nhật class này bằng kết
quả inference. Giao diện trigger và Robot API không cần thiết kế lại.

Đây mới là cầu nối tạm thời. Việc tự động chuyển kết quả model Python
sang `default_class` chưa được triển khai vì chưa có model và chưa chốt
giao thức chạy model trên Windows.

### 3.5. Thiết kế lại giao diện kết quả AI

Toàn bộ dữ liệu mẫu kiểm tra PCB đã được loại bỏ, bao gồm:

- IC;
- resistor;
- capacitor;
- số lượng linh kiện giả;
- confidence 92% cố định;
- ảnh nền xám giả;
- các bounding box mẫu;
- trạng thái GOOD/BAD/MISS không liên quan đến luồng hiện tại.

Cửa sổ kết quả mới mặc định hiển thị trạng thái `WAITING` và có sẵn các
trường:

- class được phát hiện;
- confidence;
- chu trình robot được chọn;
- thời gian inference;
- thời điểm xử lý;
- ảnh đã annotate;
- bounding box theo tọa độ ảnh nguồn;
- nút lưu ảnh kết quả.

ViewModel đã có phương thức `LoadResult(InspectionResult result)`. Sau
này Python AI client có thể chuyển kết quả inference sang
`InspectionResult` rồi mở hoặc cập nhật cửa sổ này.

### 3.6. Sửa phần Robot API trên giao diện

`robot_api.py` hiện trả `/robot/status` theo dạng:

```json
{
  "success": true,
  "robot": {
    "state": "vision_ready",
    "busy": false,
    "current_step": null,
    "motion_enabled": true,
    "queue_size": 0
  }
}
```

Trong khi một số phiên bản cũ có thể trả các trường trạng thái ngay ở cấp
ngoài. WPF hiện hỗ trợ cả hai cấu trúc bằng cách sử dụng trạng thái
`robot` nếu tồn tại, nếu không sẽ dùng dữ liệu cấp ngoài.

Giao diện robot được bổ sung:

- `VISION HOME`;
- số lượng job trong hàng đợi;
- cập nhật `motion_enabled`;
- reset đúng trạng thái khi ngắt kết nối;
- đồng bộ slider với góc servo thực tế khi người dùng không kéo slider.

Nếu robot được điều khiển từ ứng dụng khác, `CurrentAngle` và vị trí
slider có thể đồng bộ lại thông qua polling `/robot/servos`. Chức năng
này vẫn cần robot thật để xác nhận độ trễ và độ ổn định.

### 3.7. Thiết kế lại tab Conveyor

Tab băng tải không còn giả vờ điều khiển thiết bị. Giao diện hiện ghi rõ
`Conveyor hardware interface pending`.

Các thay đổi:

- tốc độ chuyển sang đơn vị mm/s;
- giá trị tham khảo là 40 mm/s;
- nút Run và Stop bị disable;
- loại bỏ nút `Servo` không rõ chức năng;
- ghi rõ slider không gửi lệnh đến phần cứng;
- hướng dẫn nhập tốc độ thật vào tab DOFBOT Trigger để tính timing.

Khi bộ điều khiển động cơ được chọn, tab này cần được kết nối với service
phần cứng thật thay vì chỉ bỏ thuộc tính `IsEnabled=False`.

### 3.8. System Log và sidebar

Các ô Camera COM, Camera Baudrate, Arduino COM và Arduino Baudrate trống
đã được loại bỏ.

Sidebar hiện mô tả kiến trúc:

- IMITECH → Windows AI;
- DOFBOT camera → Jetson;
- Jetson Robot API → DOFBOT.

System Log đã được binding với một nguồn log chung. Các ViewModel camera,
robot và vision có thể ghi thông báo vào cùng danh sách. Số lượng log
được giới hạn để tránh bộ nhớ tăng không giới hạn.

---

## 4. Các kiểm tra phần mềm đã thực hiện

### 4.1. Build Debug

Kết quả:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

Output:

`D:\capstone\WPF\WpfApp3\bin\Debug\net8.0-windows`

### 4.2. Build Release

Kết quả:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

Output:

`D:\capstone\WPF\WpfApp3\bin\Release\net8.0-windows`

### 4.3. Kiểm tra XAML và command

Build WPF đã xác nhận:

- các tệp XAML có cú pháp hợp lệ;
- các event handler được tham chiếu tồn tại;
- các class UserControl mới được tìm thấy;
- các model và API client biên dịch được;
- các RelayCommand được sinh thành công.

### 4.4. Kiểm tra placeholder cũ

Không còn các thành phần sau trong mã giao diện đang sử dụng:

- `Conveyor Simulation`;
- `Execute Model`;
- `Stop Model`;
- `Camera COM`;
- `Arduino COM`;
- dữ liệu PCB mẫu;
- resolution/FPS camera cố định.

Placeholder duy nhất được giữ có chủ đích là phần điều khiển băng tải,
với nhãn rõ ràng rằng chưa có phần cứng.

---

## 5. Các hạng mục bắt buộc kết nối thiết bị để kiểm tra

### 5.1. Camera IMITECH trên Windows

Các bước cần kiểm tra:

1. Kết nối camera và xác nhận camera xuất hiện trong MVS_STD.
2. Đóng chế độ grabbing trong MVS_STD trước khi mở WPF để tránh hai ứng
   dụng tranh quyền truy cập camera.
3. Nhấn Refresh trong tab AI Camera.
4. Chọn đúng camera và nhấn Start Camera.
5. Xác nhận trạng thái chỉ chuyển thành `Connected` khi có stream.
6. Xác nhận live feed hiển thị liên tục.
7. Nhấn Capture Image và kiểm tra ảnh được lưu.
8. Kiểm tra resolution/FPS trên overlay.
9. Stop rồi Start lại nhiều lần.
10. Chạy liên tục ít nhất 15–30 phút để kiểm tra treo luồng hoặc tăng bộ
    nhớ.

Tiêu chí đạt:

- không báo Connected khi không có frame;
- live feed và capture sử dụng cùng hình ảnh;
- không crash khi rút camera hoặc mất kết nối;
- camera được giải phóng sau khi đóng ứng dụng.

**Lưu ý:** project hiện vẫn dùng OpenCvSharp để đọc camera. Chưa có
wrapper MVS SDK trực tiếp trong WPF. Nếu camera chỉ stream ổn định qua
MVS SDK mà OpenCV không đọc được, cần viết `MvsCameraService` thay cho
`WebcamCameraService`.

### 5.2. Jetson và Robot API

Trước khi cho robot chuyển động:

1. Xác nhận máy Windows và Jetson cùng mạng.
2. Kiểm tra đúng IP của Jetson.
3. Chạy `robot_api.py` trên Jetson.
4. Mở port 7000 nếu firewall đang chặn.
5. Truy cập `/health` và xác nhận:
   - `status = ok`;
   - `robot_initialized = true`;
   - `worker_alive = true`;
   - `vision_available = true`.
6. Kết nối từ tab Robot Manual.
7. Kiểm tra `/robot/status` hiển thị đúng state, busy và current step.
8. Kiểm tra `/robot/servos` trả đủ sáu góc.

Lần kiểm tra từ máy phát triển khi lập báo cáo đã timeout tại
`192.168.137.179:7000`, nên các mục trên chưa được xác nhận.

### 5.3. Sáu servo DOFBOT

Thực hiện trên vùng làm việc không có vật cản:

1. Đọc và ghi lại góc ban đầu của sáu servo.
2. Di chuyển từng servo với bước nhỏ khoảng 3–5°.
3. Kiểm tra số màu xanh và slider cập nhật theo vị trí thật.
4. Điều khiển robot từ ứng dụng Yahboom khác và quan sát WPF.
5. Kiểm tra Open/Close gripper.
6. Kiểm tra HOME.
7. Sau khi hiệu chỉnh pose, kiểm tra VISION_HOME.
8. Kiểm tra robot từ chối lệnh thủ công khi đang busy.

Không nên kiểm tra `Apply all`, HOME hoặc VISION_HOME lần đầu với tốc độ
cao. Các góc pose trong Python vẫn là giá trị khởi đầu và cần hiệu chỉnh
theo hệ thống thật.

### 5.4. Camera DOFBOT

Quy trình an toàn:

1. Đảm bảo `auto_trigger = false`.
2. Kết nối tab DOFBOT Trigger.
3. Nhấn Start monitoring.
4. Xác nhận `Vision running = True`.
5. Xác nhận `Camera open = True`.
6. Xác nhận ảnh `/vision/frame.jpg` xuất hiện.
7. Kiểm tra hai vạch xanh trùng với hai cạnh băng tải.
8. Kiểm tra vùng entry màu vàng nằm ở đầu băng tải.
9. Đưa từng vật vào vùng quan sát và kiểm tra:
   - center;
   - relative angle;
   - Servo 5;
   - stable frames;
   - trigger latch.
10. Lấy vật ra khỏi vùng và xác nhận trigger được rearm.

Chỉ sau khi monitor-only hoạt động ổn định mới đưa robot về VISION_HOME
và thử auto-trigger.

### 5.5. Băng tải

Phần này chưa thể kiểm tra vì chưa có motor driver/service.

Cần xác định:

- loại bộ điều khiển: Arduino, PLC, driver serial, relay hay Ethernet;
- giao thức Run/Stop;
- cách đặt tốc độ;
- có encoder hay không;
- cơ chế dừng khẩn cấp;
- cơ chế xác nhận băng tải thực sự đang chạy.

Sau khi phần cứng hoàn thành cần đo tốc độ thực bằng:

```text
belt_speed_mm_s = quãng đường vật đi được / thời gian
```

Không nên lấy trực tiếp phần trăm PWM làm mm/s. Cần tạo bảng calibration
giữa tín hiệu điều khiển và tốc độ thực.

### 5.6. Mô hình AI Python trên Windows

Chưa thể kiểm tra vì model chưa được train và chưa có process/API chạy
inference.

Cần thống nhất contract tối thiểu:

```json
{
  "class_name": "metal",
  "confidence": 0.95,
  "recommended_robot_cycle": "metal",
  "inference_time_ms": 42.5,
  "bounding_boxes": []
}
```

Class trả về phải khớp với các class Robot API cho phép:

- `plastic`;
- `metal`;
- `paper`.

Sau khi có model cần kiểm tra:

1. WPF lấy frame mới nhất từ camera IMITECH.
2. Frame được gửi đến Python mà không khóa live feed.
3. Python trả class và confidence.
4. WPF cập nhật cửa sổ AI result.
5. Class được ghi vào `default_class` của Vision API.
6. Class được giữ ổn định cho đến khi camera DOFBOT tạo trigger.
7. Không sử dụng kết quả cũ cho vật tiếp theo.

### 5.7. Kiểm tra end-to-end

Chỉ thực hiện sau khi từng thiết bị đã vượt qua kiểm tra riêng.

Thứ tự đề nghị:

1. Băng tải dừng, robot chạy manual.
2. Camera IMITECH phân loại một vật.
3. Kiểm tra chu trình/class được chọn đúng.
4. Đưa robot về VISION_HOME.
5. Chạy camera DOFBOT ở monitor-only.
6. Cho vật đi qua bằng tay và kiểm tra trigger.
7. Chạy băng tải ở tốc độ thấp.
8. Xác nhận timing không báo `LATE`.
9. Bật auto-trigger.
10. Thử một vật cho mỗi chu trình.
11. Sau đó mới thử nhiều chu kỳ liên tục.

---

## 6. Các phần mềm vẫn còn cần hoàn thiện

Những mục sau không thể hoàn tất chỉ bằng việc nối thiết bị:

1. Viết `MvsCameraService` nếu OpenCV không đọc ổn định camera IMITECH.
2. Viết AI process/client để WPF gọi model Python.
3. Truyền tự động class AI sang `default_class`; hiện tại mới có lựa
   chọn thủ công để thử luồng.
4. Viết Conveyor service sau khi chọn motor driver.
5. Bổ sung khóa chế độ Manual/Automatic trên toàn giao diện. Hiện Robot
   API có motion lock khi đang busy, nhưng UI Robot Manual và Vision
   Trigger chưa chia sẻ một mode switch duy nhất.
6. Bổ sung cơ chế dừng khẩn cấp theo phần cứng. Nút Stop trên phần mềm
   không được coi là thay thế E-stop vật lý.
7. Lưu cấu hình IP, timing và camera giữa các lần mở ứng dụng.
8. Hiển thị lịch sử AI result và robot job nếu cần truy vết.

---

## 7. Kết luận

Giao diện đã được đưa từ trạng thái có nhiều dữ liệu và nút giả sang
trạng thái phản ánh đúng kiến trúc dự án:

- camera IMITECH dành cho AI;
- camera DOFBOT dành cho trigger;
- Robot API điều khiển chuyển động;
- băng tải được đánh dấu rõ là chưa có phần cứng;
- AI result đã có contract nhưng không hiển thị kết quả giả;
- Vision Trigger đã có các điểm kết nối API cần thiết;
- Robot status hỗ trợ response mới;
- cả Debug và Release đều build sạch.

Phần mềm hiện sẵn sàng cho giai đoạn kiểm tra từng thiết bị. Tuy nhiên,
chưa nên bật auto-trigger hoặc cho robot chạy cùng băng tải cho đến khi
hoàn tất các bài kiểm tra camera DOFBOT ở monitor-only, hiệu chỉnh
VISION_HOME, giới hạn servo, tốc độ băng tải và kiểm tra một chu kỳ gắp
đơn lẻ trong vùng làm việc an toàn.
