# BÁO CÁO CHI TIẾT CÁC CHỈNH SỬA HỆ THỐNG CAMERA TRIGGER VÀ ĐIỀU KHIỂN DOFBOT

**Dự án:** Hệ thống kiểm tra và phân loại pin sử dụng camera, AI, băng tải và tay robot DOFBOT  
**Ngày cập nhật:** 30/07/2026  
**Phạm vi báo cáo:** Các thay đổi được thực hiện cho Robot API, cơ chế camera trigger, hiệu chỉnh Servo 5, tính toán thời gian đồng bộ với băng tải, script triển khai và tài liệu dự án.

## 1. Bối cảnh và mục tiêu của đợt chỉnh sửa

Thiết kế ban đầu của hệ thống dự kiến sử dụng camera để xác định đầy đủ vị trí
của vật thể, chuyển tọa độ pixel trên ảnh sang tọa độ thực, sau đó sử dụng
inverse kinematics để tính lại góc của nhiều servo. Phương án này có khả năng
xử lý nhiều vị trí khác nhau, nhưng yêu cầu khối lượng hiệu chỉnh lớn, bao gồm
hiệu chỉnh camera, pixel-to-world, hệ tọa độ tay robot, chiều cao vật thể, sai
số cơ khí và thuật toán động học nghịch. Đối với mô hình hiện tại, vị trí gắp
trên băng tải là một vị trí cố định và các pose của robot đã được thiết lập từ
trước. Vì vậy, giải bài toán định vị ba chiều tổng quát là chưa cần thiết và có
thể làm hệ thống phức tạp hơn mức cần thiết.

Kiến trúc mới tận dụng hai thanh dẫn hướng ở hai cạnh băng tải để căn chỉnh vật
thể bằng cơ khí. Camera DOFBOT được đưa tới một pose quan sát cố định có tên
`VISION_HOME` và hướng về đầu băng tải. Khi vật thể đi đủ sâu vào vùng đầu của
khung hình, camera tạo một sự kiện trigger. Robot tiếp tục sử dụng các pose cố
định `PICK_ABOVE`, `PICK_DOWN`, `PICK_LIFT` và các pose đặt vật vào hộp. Thành
phần duy nhất được hiệu chỉnh theo ảnh là Servo 5, tương ứng với chuyển động
xoay cổ tay. Ngoài ra, hệ thống tính thời gian từ lúc phát hiện tới khi vật đi
đến target để quyết định robot cần bắt đầu chuyển động ngay hay chờ một khoảng
thời gian.

Mục tiêu của đợt chỉnh sửa này là tạo phần lõi phần mềm cho kiến trúc mới,
nhưng vẫn bảo đảm robot không tự chuyển động trước khi camera, góc Servo 5 và
timing được hiệu chỉnh trên thiết bị thật.

## 2. Bổ sung pose quan sát `VISION_HOME`

File `D:\capstone\Dofbot\robot_api.py` đã được bổ sung pose
`VISION_HOME`. Pose này đại diện cho vị trí quan sát đầu băng tải. Trong cấu
hình ban đầu, các góc Servo 1–5 được đặt tương tự HOME và Servo 6 được đặt ở
góc mở kẹp. Mục đích của việc mở kẹp sẵn là giảm thời gian chuẩn bị sau khi
camera phát hiện vật. Nếu robot đã ở vị trí quan sát và kẹp đã mở, chu trình
gắp không cần thực hiện lại lệnh HOME và OPEN_GRIPPER.

Các góc hiện tại của `VISION_HOME` mới chỉ là giá trị khởi đầu. Chúng phải được
hiệu chỉnh trực tiếp trên DOFBOT để camera nhìn rõ vùng đầu băng tải, hai thanh
dẫn hướng và toàn bộ phần vật thể cần thiết cho việc đo góc. Sau khi tìm được
góc phù hợp, sáu giá trị servo thực tế cần được ghi lại và cập nhật vào
`POSES["VISION_HOME"]`.

Một endpoint mới đã được thêm:

```text
POST /robot/vision-home
```

Endpoint này sử dụng cùng cơ chế `motion_lock` của Robot API để tránh xung đột
với job đang chạy. Khi bắt đầu, trạng thái robot được đổi thành
`positioning_for_vision`. Sau khi tới pose thành công, trạng thái được đổi
thành `vision_ready`. Module vision chỉ được phép tự tạo job gắp khi robot có
trạng thái `vision_ready` và `busy=false`. Điều kiện này ngăn camera trigger
robot trong trường hợp robot đang ở một vị trí không phù hợp.

## 3. Mở rộng hàm di chuyển pose để hỗ trợ Servo 5 động

Hàm `move_pose` trước đây lấy trực tiếp danh sách góc từ biến `POSES` và gửi
toàn bộ sáu góc xuống `Arm_Lib`. Hàm này đã được mở rộng với tham số tùy chọn
`wrist_angle`.

Khi không truyền `wrist_angle`, hành vi cũ được giữ nguyên. Khi có
`wrist_angle`, hàm tạo một bản sao của pose, kiểm tra góc hợp lệ và chỉ thay
phần tử thứ năm trong danh sách. Cách tạo bản sao rất quan trọng vì nó không
làm thay đổi vĩnh viễn pose gốc trong từ điển `POSES`. Nhờ vậy, hai job liên
tiếp có góc pin khác nhau sẽ không ảnh hưởng lẫn nhau.

Góc Servo 5 tiếp tục được kiểm tra bằng hàm `validate_angle`, do đó giá trị
ngoài khoảng 0–180 độ sẽ bị từ chối. Các giới hạn hẹp hơn dành riêng cho cổ tay
được xử lý ở module vision trước khi job được tạo.

Trong chu trình gắp, `wrist_angle` chỉ được sử dụng tại ba pose:

```text
PICK_ABOVE
PICK_DOWN
PICK_LIFT
```

Servo 5 được xoay khi robot tiếp cận phía trên vật và được giữ cùng hướng trong
lúc hạ xuống, đóng kẹp và nâng vật lên. Robot không thực hiện xoay cổ tay trong
lúc kẹp đang tiếp xúc với pin. Khi di chuyển tới hộp phân loại, robot trở lại
góc Servo 5 đã cấu hình trong pose đặt vật. Cách xử lý này giảm nguy cơ làm pin
trượt hoặc xoay ngoài ý muốn.

## 4. Bổ sung job có thời gian chờ và trạng thái prepositioned

Endpoint `POST /robot/pick` đã được mở rộng để nhận payload như sau:

```json
{
  "class_name": "metal",
  "wrist_angle": 105,
  "start_delay_ms": 500,
  "prepositioned": true
}
```

Trường `wrist_angle` là góc Servo 5 đề xuất từ camera. Trường
`start_delay_ms` là thời gian robot chờ trước khi bắt đầu chu trình gắp. Giá
trị này được giới hạn từ 0 tới `MAX_PICK_START_DELAY_MS`, hiện được đặt là
30.000 ms. Việc đặt giới hạn ngăn một cấu hình sai làm job chiếm robot quá lâu.

Trường `prepositioned` cho biết robot đã đứng ở `VISION_HOME` và kẹp đã mở. Khi
giá trị là `true`, chu trình bỏ qua bước trở lại HOME và mở kẹp. Khi giá trị là
`false`, chu trình giữ hành vi cũ để các lệnh gắp thủ công vẫn tương thích.

Nếu `start_delay_ms` lớn hơn 0, robot chuyển sang trạng thái
`waiting_for_target`, đồng thời `current_step` được đặt thành
`WAITING_FOR_TARGET`. Sau khi hết thời gian chờ, robot chuyển sang trạng thái
`busy` và bắt đầu pose gắp. Các trường mới cũng được lưu trong thông tin job,
bao gồm `wrist_angle`, `start_delay_ms`, `prepositioned` và `source`. Trường
`source` giúp phân biệt job được tạo từ API thông thường hay từ camera vision.

Sau khi một job từ camera hoàn tất, robot trở lại `VISION_HOME` và trạng thái
được đặt lại thành `vision_ready`, thay vì `idle`. Điều này cho phép module
camera tự động re-arm và chờ vật tiếp theo.

## 5. Tách logic tạo job thành hàm dùng chung

Logic tạo job trước đây nằm hoàn toàn trong route `/robot/pick`. Logic này đã
được tách thành hàm `enqueue_pick_job`. Hàm thực hiện các bước:

1. Kiểm tra class có tồn tại trong `CLASS_TO_BOX`.
2. Kiểm tra và làm tròn `wrist_angle`.
3. Kiểm tra giới hạn `start_delay_ms`.
4. Tạo hoặc kiểm tra `job_id`.
5. Lưu toàn bộ thông tin job vào `jobs`.
6. Đưa dữ liệu cần thiết vào `job_queue`.
7. Cập nhật trạng thái hệ thống và ghi log.

Route HTTP và module camera đều gọi cùng hàm này. Việc dùng chung một đường xử
lý tránh tình trạng camera tạo job theo quy tắc khác với WPF hoặc AI.

## 6. Module `vision_trigger.py`

Một file mới `D:\capstone\Dofbot\vision_trigger.py` đã được tạo. Module này
chứa lớp `VisionTriggerController`, chịu trách nhiệm đọc camera, phát hiện vật
đi vào vùng đầu khung hình, ước lượng hướng vật, tính Servo 5, tính timing và
tạo job thông qua callback do Robot API cung cấp.

OpenCV và NumPy được import theo cơ chế có kiểm soát. Nếu container chưa có
OpenCV Python, Robot API vẫn có thể báo rõ `opencv_available=false`, và lệnh
khởi động vision trả về lỗi thay vì làm toàn bộ Robot API bị dừng.

Cấu hình mặc định được biểu diễn bằng `VisionTriggerConfig`. Các nhóm cấu hình
chính gồm:

- Camera index và kích thước frame.
- Vị trí tương đối của hai thanh dẫn.
- Cạnh ảnh mà vật đi vào.
- Độ sâu vùng entry.
- Diện tích contour tối thiểu và tối đa.
- Số frame ổn định trước khi trigger.
- Số frame trống để re-arm.
- Tham số background subtraction.
- Góc hướng băng tải trong ảnh.
- Góc Servo 5 tham chiếu, dấu hiệu chỉnh và giới hạn.
- Tốc độ băng tải, khoảng cách tới target và thời gian robot.
- Class mặc định cho giai đoạn chưa kết nối AI.
- Cờ `auto_trigger`.

Cấu hình được lưu tại `/root/vision_config.json` sau lần cập nhật đầu tiên.
Điều này giúp các thông số camera và timing được giữ lại sau khi Robot API
khởi động lại.

## 7. Phát hiện vật trong hành lang giữa hai thanh dẫn

Phiên bản đầu sử dụng `cv2.createBackgroundSubtractorMOG2` để tách vật thể đang
chuyển động khỏi nền băng tải. Camera phải đứng yên ở `VISION_HOME` để mô hình
nền hoạt động ổn định.

Hai tham số `rail_left_ratio` và `rail_right_ratio` định nghĩa hành lang hợp lệ
giữa hai thanh dẫn. Mọi chuyển động ngoài hành lang này bị loại bỏ. Sau đó mask
được xử lý bằng phép morphology open và close để loại nhiễu nhỏ và nối các
vùng thuộc cùng một vật.

Các contour được lọc theo diện tích. Contour lớn nhất có tâm nằm trong entry
zone được chọn làm ứng viên. Entry zone có thể nằm ở cạnh trên, dưới, trái hoặc
phải tùy hướng camera và hướng băng tải. Vật phải được theo dõi ổn định qua số
frame được cấu hình trong `stable_frames`. Khoảng cách giữa tâm ở hai frame
liên tiếp cũng được kiểm tra để hạn chế việc nối hai vật khác nhau thành cùng
một detection.

Sau khi trigger, hệ thống đặt `trigger_latched=true`. Trigger chỉ được mở lại
khi vật đã rời vùng camera đủ số frame, robot đã hoàn thành job và trở lại
`vision_ready`. Đây là cơ chế chống một viên pin tạo nhiều job liên tiếp.

## 8. Ước lượng góc pin và tính Servo 5

Contour hợp lệ được đưa vào `cv2.minAreaRect` để nhận rotated bounding box.
Góc trục dài của vật được chuẩn hóa về khoảng 0–180 độ. Sau đó góc tương đối
được tính bằng:

```text
relative_angle = object_angle - conveyor_angle
```

Kết quả được chuẩn hóa về khoảng -90 tới 90 độ. Servo 5 được tính theo:

```text
wrist_angle =
    wrist_reference_angle
    + wrist_direction × relative_angle
```

Độ hiệu chỉnh được giới hạn bởi `max_wrist_correction_deg`, sau đó góc cuối
cùng tiếp tục được clamp trong `wrist_min_angle` và `wrist_max_angle`. Có thể
tắt hoàn toàn phần hiệu chỉnh bằng `use_wrist_correction=false`. Khi hai thanh
dẫn căn pin đủ tốt, tùy chọn này cho phép sử dụng Servo 5 cố định.

Trong bài kiểm tra logic, góc vật 105 độ với hướng băng tải 90 độ tạo góc tương
đối 15 độ và Servo 5 là 105 độ khi góc tham chiếu bằng 90 và hướng hiệu chỉnh
bằng 1. Kết quả phù hợp với công thức thiết kế.

## 9. Tính toán thời gian đồng bộ

Module vision sử dụng bốn thông số:

- `belt_speed_mm_s`: tốc độ băng tải thực tế.
- `distance_to_pick_mm`: khoảng cách từ vùng entry đến target.
- `robot_time_to_grip_ms`: thời gian robot tới thời điểm đóng kẹp.
- `processing_margin_ms`: thời gian AI, camera, mạng và dự phòng.

Thời gian vật tới target:

```text
arrival_ms =
    distance_to_pick_mm / belt_speed_mm_s × 1000
```

Thời gian robot cần chờ:

```text
start_delay_ms =
    arrival_ms
    - robot_time_to_grip_ms
    - processing_margin_ms
```

Nếu kết quả âm, trigger bị đánh dấu `late=true`. Khi
`reject_late_trigger=true`, module không tạo job. Người vận hành phải giảm tốc
băng tải, đặt camera xa target hơn hoặc dừng băng tải.

Với cấu hình mẫu 200 mm, 40 mm/s, thời gian robot 4.150 ms và dự phòng 350 ms,
vật cần 5.000 ms để tới target và robot cần chờ 500 ms. Phần tính toán này đã
được kiểm tra độc lập trên máy phát triển.

## 10. Endpoint vision và ảnh debug

Các endpoint mới gồm:

```text
GET  /vision/status
GET  /vision/config
POST /vision/config
POST /vision/start
POST /vision/stop
POST /vision/reset-trigger
GET  /vision/frame.jpg
```

Ảnh debug vẽ hai đường giới hạn hành lang, hình chữ nhật entry zone, rotated
bounding box của vật, góc tương đối và góc Servo 5 đề xuất. Ảnh cũng hiển thị
`MONITOR ONLY` hoặc `AUTO`.

Quan trọng nhất, `auto_trigger` mặc định là `false`. Lệnh `/vision/start` chỉ
mở camera và phân tích ảnh; nó không ra lệnh cho robot. Chỉ khi người vận hành
đã hiệu chỉnh xong và chủ động cập nhật `auto_trigger=true`, module mới có thể
tạo job. Ngay cả lúc đó, robot vẫn phải ở trạng thái `vision_ready`.

## 11. Cập nhật script triển khai

File `D:\capstone\Dofbot\deploy.ps1` đã được cập nhật để triển khai cả
`robot_api.py` và `vision_trigger.py`. Script kiểm tra hai file trên Windows,
upload lên Jetson, ghi vào `/root` trong container `kind_pare`, kiểm tra file
không rỗng và chạy `python3 -m py_compile` cho cả hai file. Thông tin file sau
deploy cũng được hiển thị để hỗ trợ kiểm tra phiên bản.

Hướng dẫn chi tiết được lưu trong
`D:\capstone\Dofbot\VISION_TRIGGER_README.md`. Roadmap của toàn dự án được
khôi phục và cập nhật tại
`D:\capstone\WPF\WpfApp3\PROJECT_ROADMAP.md`.

## 12. Kết quả kiểm tra và trạng thái triển khai

Cả `robot_api.py` và `vision_trigger.py` đã vượt qua kiểm tra cú pháp Python.
Script `deploy.ps1` đã vượt qua kiểm tra cú pháp PowerShell. Các endpoint mới
đã được kiểm tra tĩnh qua cây cú pháp. Logic timing và tính Servo 5 đã được
kiểm tra độc lập. Logic tạo job đã được kiểm tra bằng các module giả, không
khởi tạo `Arm_Device` và không gửi lệnh tới robot.

Runtime Flask đầy đủ chưa được kiểm tra trên Windows vì môi trường Python local
không có Flask. Đây không phải môi trường chạy chính thức; Flask và `Arm_Lib`
được sử dụng trong container Jetson.

Việc deploy lên Jetson chưa thực hiện được vì kết nối SSH tới
`192.168.137.179:22` bị timeout. Do đó, báo cáo không khẳng định camera thật đã
mở được, thiết bị camera là index 0 hay thuật toán đã nhận đúng pin. Các nội
dung đó phải được xác nhận trên phần cứng.

## 13. Giới hạn hiện tại và công việc tiếp theo

Phiên bản hiện tại dùng background subtraction, vì vậy camera phải đứng yên và
môi trường ánh sáng không được thay đổi quá mạnh. Hai thanh dẫn mới được biểu
diễn bằng tỷ lệ trên ảnh; hệ thống chưa tự phát hiện đường rail. AI phân loại
chưa được kết nối, nên vision đang sử dụng `default_class`, mặc định là
`metal`. Arduino và lệnh RUN/STOP băng tải cũng chưa được tích hợp.

Bước tiếp theo là bật Jetson và Ethernet, deploy hai file, đặt
`auto_trigger=false`, đưa robot tới `VISION_HOME`, khởi động camera và mở
`/vision/frame.jpg`. Sau đó cần cung cấp ảnh thật để hiệu chỉnh hành lang, entry
zone, contour, hướng băng tải và góc cổ tay. Tiếp theo cần đo tốc độ băng tải,
khoảng cách vật lý và thời gian robot thật. Chỉ sau khi monitor-only hoạt động
ổn định qua nhiều thử nghiệm mới được bật auto-trigger.

## 14. Kết luận

Đợt chỉnh sửa đã chuyển kiến trúc hệ thống từ bài toán định vị robot tổng quát
sang mô hình trigger theo sự kiện, pose cố định và Servo 5 động. Hướng mới tận
dụng căn chỉnh cơ khí của băng tải, giảm đáng kể yêu cầu calibration và giữ
nguyên các pose robot đã thiết lập. Robot API hiện có khả năng nhận góc cổ tay,
chờ theo timing, bỏ qua bước chuẩn bị dư thừa và trở lại vị trí quan sát.
Module vision cung cấp đầy đủ nền tảng để phát hiện vật, tính góc, tính timing,
chống trigger trùng và quan sát qua ảnh debug.

Các cơ chế an toàn được đặt làm mặc định: camera có thể chạy monitor-only,
auto-trigger bị tắt, trigger trễ có thể bị từ chối, góc Servo 5 bị giới hạn và
robot phải ở `VISION_HOME`. Phần mã nguồn đã sẵn sàng cho giai đoạn deploy và
hiệu chỉnh trên thiết bị thật, nhưng chưa được xem là hoàn thành vận hành cho
đến khi kết nối Jetson, camera, băng tải và robot được kiểm tra trực tiếp.
