# Roadmap hệ thống kiểm tra và phân loại pin

Cập nhật: 2026-07-30

## Mục tiêu vận hành

```text
Băng tải đưa pin vào vùng camera
→ camera xác nhận pin đã đi vào khung
→ AI phân loại và camera ước lượng hướng pin
→ tính thời gian pin tới target
→ robot chạy các pose gắp cố định
→ chỉ hiệu chỉnh Servo 5
→ đặt pin vào vị trí phân loại
→ robot trở về VISION_HOME
```

## Kiến trúc đã thống nhất

- Hai thanh dẫn của băng tải căn chỉnh vị trí và hướng pin bằng cơ khí.
- Camera DOFBOT đặt tại pose cố định `VISION_HOME`.
- Vùng đầu khung hình được dùng làm trigger.
- Pin phải xuất hiện ổn định qua nhiều frame trước khi trigger.
- Servo 1–4 sử dụng pose cố định đã hiệu chỉnh.
- Servo 5 được hiệu chỉnh theo góc pin tương đối với hướng băng tải.
- Servo 6 tiếp tục đóng/mở kẹp theo chu trình.
- Giai đoạn đầu ưu tiên dừng băng tải nếu timing liên tục chưa ổn định.
- Không cần pixel-to-world hoặc inverse kinematics tổng quát trong kiến trúc
  hiện tại.

## Đã hoàn thành trong mã nguồn

### WPF

- [x] Giao diện camera cơ bản.
- [x] Tab điều khiển thủ công DOFBOT.
- [x] Kết nối WPF với Robot API.
- [x] Đọc trạng thái và góc sáu servo.
- [x] Điều khiển servo trực tiếp khi kéo slider.
- [x] Giới hạn tốc độ gửi lệnh slider.
- [x] HOME, mở kẹp, đóng kẹp và reset.

### Robot API

- [x] `GET /health`.
- [x] `GET /robot/status`.
- [x] `GET /robot/servos`.
- [x] `POST /robot/servo`.
- [x] `POST /robot/servos`.
- [x] `POST /robot/pick`.
- [x] Hàng đợi job và các pose gắp/phân loại cố định.
- [x] Pose mặc định `VISION_HOME`.
- [x] Endpoint `POST /robot/vision-home`.
- [x] Job gắp hỗ trợ `wrist_angle`.
- [x] Chỉ thay Servo 5 trong `PICK_ABOVE`, `PICK_DOWN`, `PICK_LIFT`.
- [x] Job gắp hỗ trợ `start_delay_ms`.
- [x] Job camera hỗ trợ `prepositioned=true`.

### Vision trigger

- [x] Module `vision_trigger.py`.
- [x] Mặc định `auto_trigger=false`.
- [x] Vùng hành lang giữa hai thanh dẫn.
- [x] Vùng nhận diện ở cạnh khung hình.
- [x] Xác nhận vật ổn định qua nhiều frame.
- [x] Chống trigger trùng.
- [x] Ước lượng hướng bằng rotated bounding box.
- [x] Tính góc Servo 5 có giới hạn.
- [x] Tính thời gian vật đến target.
- [x] Phát hiện trường hợp trigger quá trễ.
- [x] Ảnh JPEG debug có rail, entry zone và bounding box.
- [x] Endpoint trạng thái, cấu hình, start/stop và reset trigger.
- [x] Script deploy bao gồm cả `vision_trigger.py`.

## Việc cần làm tiếp theo

### 1. Deploy và kiểm tra Robot API

- [ ] Dừng Robot API cũ.
- [ ] Chạy `D:\capstone\Dofbot\deploy.ps1`.
- [ ] Khởi động lại Robot API.
- [ ] Kiểm tra `vision_available=true` trong `/health`.
- [ ] Kiểm tra `/vision/status`.
- [ ] Tạo dịch vụ `dofbot-api.service` tự khởi động trên Jetson.

### 2. Hiệu chỉnh VISION_HOME

- [ ] Đưa robot tới góc quan sát thực tế.
- [ ] Ghi lại sáu góc servo.
- [ ] Cập nhật pose `VISION_HOME`.
- [ ] Bảo đảm camera nhìn rõ đầu băng tải và hai thanh dẫn.
- [ ] Bảo đảm kẹp được mở sẵn.
- [ ] Kiểm tra robot trở lại đúng pose sau mỗi job.

### 3. Hiệu chỉnh camera ở monitor-only

- [ ] Xác nhận camera index trên Jetson.
- [ ] Khởi động `/vision/start` với `auto_trigger=false`.
- [ ] Mở `/vision/frame.jpg`.
- [ ] Hiệu chỉnh `rail_left_ratio`.
- [ ] Hiệu chỉnh `rail_right_ratio`.
- [ ] Chọn đúng `entry_edge`.
- [ ] Hiệu chỉnh `entry_zone_ratio`.
- [ ] Hiệu chỉnh `min_contour_area`.
- [ ] Kiểm tra một pin được phát hiện ổn định.
- [ ] Kiểm tra không trigger khi băng tải trống.
- [ ] Kiểm tra không trigger lặp cùng một pin.

### 4. Hiệu chỉnh Servo 5

- [ ] Lắp hai thanh dẫn để giảm góc lệch cơ khí.
- [ ] Xác định `conveyor_angle_deg`.
- [ ] Xác định `wrist_reference_angle`.
- [ ] Xác định dấu `wrist_direction` là `1` hay `-1`.
- [ ] Xác định giới hạn Servo 5 an toàn.
- [ ] Xác định độ hiệu chỉnh tối đa.
- [ ] Thử nhiều góc pin trong monitor-only.
- [ ] Chỉ bật chuyển động sau khi góc đề xuất chính xác.

### 5. Hiệu chỉnh timing

- [ ] Đo tốc độ băng tải thực tế `belt_speed_mm_s`.
- [ ] Đo khoảng cách từ entry zone tới target `distance_to_pick_mm`.
- [ ] Đo thời gian robot tới lúc đóng kẹp `robot_time_to_grip_ms`.
- [ ] Chọn `processing_margin_ms`.
- [ ] Kiểm tra `start_delay_ms`.
- [ ] Di chuyển camera xa hơn hoặc giảm tốc nếu `late=true`.
- [ ] Thử với một pin mỗi chu kỳ.
- [ ] Đánh giá sai số sau ít nhất 20 chu kỳ.

### 6. Kết nối Arduino và băng tải

- [ ] Xác định COM và baudrate.
- [ ] Tạo service `SerialPort`.
- [ ] Lệnh RUN.
- [ ] Lệnh STOP.
- [ ] Lệnh SET_SPEED.
- [ ] Phản hồi `CONVEYOR_STOPPED`.
- [ ] Dừng băng tải khi camera, AI hoặc robot lỗi.
- [ ] Nếu chạy liên tục không ổn định, dừng băng tải trước khi gắp.

### 7. Kết nối AI thật

- [ ] Thay dữ liệu AI mẫu trong WPF.
- [ ] Gửi frame tới model.
- [ ] Nhận class và confidence.
- [ ] Ánh xạ `dented` sang chu trình gắp và vị trí thả DENTED.
- [ ] Ánh xạ `scratched` sang chu trình gắp và vị trí thả SCRATCHED.
- [ ] Ánh xạ `swollen` sang chu trình gắp và vị trí thả SWOLLEN.
- [ ] Ánh xạ `normal` sang hành động PASS; không tạo robot job và để pin đi tới cuối băng tải.
- [ ] Không trigger nếu confidence dưới ngưỡng.
- [ ] Không dùng `default_class` sau khi AI đã kết nối.

### 8. Tích hợp WPF

- [ ] Tab Auto/Vision.
- [ ] Hiển thị ảnh debug từ `/vision/frame.jpg`.
- [ ] Hiển thị camera open/running.
- [ ] Hiển thị detection, angle và wrist angle.
- [ ] Hiển thị arrival time và start delay.
- [ ] Nút Move to VISION_HOME.
- [ ] Nút Start/Stop monitor.
- [ ] Công tắc Auto có xác nhận an toàn.
- [ ] Hiển thị lỗi late trigger.
- [ ] Hiển thị trạng thái băng tải và job robot.

### 9. Khởi động một lần nhấp

- [ ] Robot API tự chạy trên Jetson.
- [ ] WPF kiểm tra `/health` khi mở.
- [ ] WPF khởi động AI service.
- [ ] WPF kết nối camera công nghiệp.
- [ ] WPF kết nối Arduino.
- [ ] Màn hình tổng hợp trạng thái Ready/Error.
- [ ] Publish self-contained cho workstation.

### 10. An toàn

- [ ] Xác định giới hạn góc từng servo.
- [ ] Một controller duy nhất được phép điều khiển robot.
- [ ] E-stop vật lý trong tầm tay.
- [ ] Nút Stop phần mềm.
- [ ] Timeout cho camera, AI, Arduino và robot.
- [ ] Auto mặc định tắt sau khi khởi động.
- [ ] Ghi log và lưu ảnh khi có lỗi.

## Bước đang thực hiện

Giai đoạn monitor-only đã hoàn tất. Các bước hiệu chỉnh chuyển động và tích hợp
cuối được tạm hoãn cho đến khi có đầy đủ mô hình AI và phần cứng băng tải.

## Ghi chú bàn giao sau giai đoạn Monitor-only (2026-08-03)

### Trạng thái đã xác nhận

- [x] Robot API và Vision API chạy qua `dofbot-robot-api.service`.
- [x] Camera DOFBOT mở được và xử lý ổn định khoảng 20-26 FPS.
- [x] Entry Zone là một vùng chính giữa khung hình.
- [x] Băng tải trống trong 30 giây không tạo trigger giả.
- [x] Vật đi vào Entry Zone tạo trigger mới.
- [x] `trigger_latched` ngăn một vật tạo nhiều trigger.
- [x] Monitor-only không tạo robot job khi `auto_trigger=false`.

### Trạng thái phải giữ trong thời gian chờ phần cứng

- Giữ `auto_trigger=false`.
- Chưa sử dụng góc Servo 5 do camera đề xuất để điều khiển tự động.
- `default_class=metal` chỉ là placeholder, không phải kết quả AI thật.
- Chưa coi các giá trị timing hiện tại là thông số băng tải thật.
- Đồng bộ giờ Jetson trước các bài thử cần đối chiếu log Windows/Jetson.

### Tạm hoãn đến khi có đầy đủ phần cứng băng tải

- [ ] Dừng băng tải và dọn sạch vùng robot trước khi hiệu chỉnh.
- [ ] Đưa robot tới pose quan sát thực tế và ghi lại đủ sáu góc servo.
- [ ] Cập nhật, triển khai và xác nhận pose `VISION_HOME`.
- [ ] Xác nhận camera nhìn rõ Entry Zone và cánh tay không chắn băng tải.
- [ ] Xác nhận kẹp mở sẵn và robot trở về đúng `VISION_HOME` sau mỗi job.
- [ ] Lắp và hiệu chỉnh hai cạnh dẫn hướng cơ khí của băng tải.
- [ ] Thử pin thẳng, lệch trái và lệch phải mà không đưa tay vào Entry Zone.
- [ ] Hiệu chỉnh `conveyor_angle_deg`, `wrist_reference_angle` và `wrist_direction`.
- [ ] Xác định giới hạn Servo 5 và `max_wrist_correction_deg` an toàn.
- [ ] Đo tốc độ băng tải thật `belt_speed_mm_s` bằng nhiều lần thử.
- [ ] Đo `distance_to_pick_mm` từ tâm Entry Zone đến tâm điểm gắp.
- [ ] Đo `robot_time_to_grip_ms` từ lúc robot bắt đầu chạy đến lúc đóng kẹp.
- [ ] Chọn và kiểm chứng `processing_margin_ms` cùng `start_delay_ms`.
- [ ] Kết nối driver băng tải/Arduino: RUN, STOP, SET_SPEED và phản hồi trạng thái.
- [ ] Dừng băng tải khi camera, AI hoặc robot báo lỗi.
- [ ] Đánh giá sai số timing và khả năng gắp sau ít nhất 20 chu kỳ.

### Tích hợp mô hình AI đã huấn luyện

- [x] Xác nhận `best.pt` là YOLO detect với bốn class: `dented`, `normal`, `scratched`, `swollen`.
- [x] Kiểm tra tập test 106 ảnh với `conf=0.40`, `max_det=1`: đúng 104/106 ảnh.
- [ ] Kết nối ảnh IMITECH trên Windows với mô hình YOLO thật.
- [ ] Chuẩn hóa kết quả gồm class, confidence, timestamp và mã vật thể/chu kỳ.
- [ ] Đặt ngưỡng confidence; kết quả dưới ngưỡng không được tạo chu trình robot.
- [ ] Tạo ba chu trình robot cố định cho `dented`, `scratched`, `swollen`.
- [ ] Tạo hành động PASS cho `normal`; không gọi chu trình gắp.
- [ ] Loại bỏ việc sử dụng `default_class` sau khi AI thật đã kết nối.
- [ ] Chống dùng kết quả AI cũ cho vật thể tiếp theo bằng timestamp/timeout.
- [ ] Xếp kết quả AI vào hàng đợi FIFO để khớp đúng thứ tự pin tới camera DOFBOT.
- [ ] Khi DOFBOT trigger: lấy một kết quả hợp lệ; enqueue robot job nếu là lỗi, chỉ ghi log nếu là `normal`.
- [ ] Hiển thị class, confidence và trạng thái AI thật trên WPF/System Log.

### Quyết định luồng phân loại bốn nhánh

```text
dented    -> DOFBOT gắp -> vị trí DENTED
scratched -> DOFBOT gắp -> vị trí SCRATCHED
swollen   -> DOFBOT gắp -> vị trí SWOLLEN
normal    -> PASS       -> đi tới khay cuối băng tải
```

Camera IMITECH và YOLO quyết định nhánh phân loại. Camera DOFBOT chỉ quyết định
thời điểm vật tới vùng thực thi. Một kết quả `normal` vẫn phải tiêu thụ đúng một
sự kiện DOFBOT trigger để hàng đợi không bị lệch, nhưng tuyệt đối không tạo robot
job. Nếu không có kết quả AI hợp lệ, confidence dưới ngưỡng, robot bận quá thời
gian cho phép hoặc thứ tự vật không còn chắc chắn thì hệ thống phải dừng băng tải
thay vì đoán class.

### Giai đoạn Auto Trigger cuối cùng

- [ ] Chỉ bắt đầu khi `robot_ready=true`, pose, Servo 5 và timing đã được xác nhận.
- [ ] Thử khô robot với băng tải dừng và một pin duy nhất.
- [ ] Bật Auto Trigger có kiểm soát, E-stop và công tắc nguồn trong tầm tay.
- [ ] Thử một pin cho mỗi chu kỳ trước khi cho băng tải chạy liên tục.
- [ ] Xác nhận trigger không lặp, không nhận job khi robot bận và xử lý `late=true`.
- [ ] Kiểm tra va chạm, timeout, mất kết nối và quy trình dừng an toàn.
- [ ] Chạy thử tích hợp IMITECH -> YOLO -> chu trình robot và DOFBOT camera -> thời điểm gắp.
- [ ] Lưu log, ảnh và kết quả của ít nhất 20 chu kỳ tích hợp để đánh giá.

### Điều kiện để tiếp tục roadmap

Tiếp tục từ bước hiệu chỉnh `VISION_HOME`, không bật Auto Trigger ngay. Khi quay
lại cần chuẩn bị: băng tải và bộ điều khiển, kích thước/khoảng cách thực tế, mô
hình YOLO cùng danh sách class, các chu trình robot tương ứng và phương án E-stop.
