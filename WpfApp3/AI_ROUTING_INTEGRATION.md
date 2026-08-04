# Tích hợp phân loại pin 4 lớp

## Luồng đã triển khai

1. Camera IMITECH và YOLOv8 trên Windows tạo một kết quả cho mỗi pin.
2. Kết quả được gửi theo FIFO tới `POST /vision/classifications` trên Jetson.
3. Camera DOFBOT chỉ phát hiện thời điểm pin đi vào Entry Zone.
4. Mỗi trigger hợp lệ lấy đúng một kết quả đầu hàng đợi.
5. `normal` tạo hành động `PASS`, không tạo robot job.
6. `dented`, `scratched`, `swollen` chọn ba chu trình gắp và ba vị trí thả riêng.

## Contract gửi từ Windows AI

```json
{
  "inspection_id": "unique-id-for-one-battery",
  "class_name": "dented",
  "confidence": 0.91,
  "source": "windows_ai"
}
```

Class hợp lệ: `dented`, `normal`, `scratched`, `swollen`. Ngưỡng mặc định là
`0.40`. Kết quả hết hạn sau 30 giây để tránh dùng kết quả cũ cho pin tiếp theo.
Hai giá trị này nằm trong `VisionTriggerConfig`.

Các endpoint:

- `POST /vision/classifications`: thêm một kết quả.
- `GET /vision/classifications`: xem số lượng và kết quả kế tiếp.
- `DELETE /vision/classifications`: xóa hàng đợi khi dừng hoặc reset hệ thống.
- `GET /vision/status`: trạng thái camera, trigger và hàng đợi trong một response.
- `GET /robot/routing`: ánh xạ bốn class và trạng thái hiệu chỉnh chu trình.

## Trạng thái an toàn hiện tại

`Auto Trigger` vẫn mặc định tắt. Ba cấu hình thả có tên
`DROP_DENTED_*`, `DROP_SCRATCHED_*`, `DROP_SWOLLEN_*`, nhưng đều có
`calibrated: false`. Robot API sẽ từ chối enqueue ba chu trình lỗi cho tới khi
các pose được đo, thử chậm và xác nhận không va chạm. Không bật
`ALLOW_UNCALIBRATED_DEFECT_CYCLES` để chạy thật.

Phần WPF có nút `Enqueue test result` để kiểm tra contract khi AI service chưa
hoàn tất. Nút này dùng đúng endpoint mà AI service thật sẽ dùng, nhưng gắn
`source=wpf_test` để phân biệt trong log.

## Phần còn cần phần cứng hoặc AI service

- Kết nối frame IMITECH trực tiếp với `best.pt` và tự động POST kết quả.
- Đo và xác nhận ba cặp pose `above/drop`; đổi `calibrated` riêng từng class.
- Hiệu chỉnh TTL theo thời gian pin đi từ camera IMITECH tới camera DOFBOT.
- Kết nối driver băng tải để dừng khi thiếu kết quả AI, trigger trễ hoặc robot lỗi.
- Test ít nhất 20 pin liên tiếp để xác nhận FIFO không lệch thứ tự.
