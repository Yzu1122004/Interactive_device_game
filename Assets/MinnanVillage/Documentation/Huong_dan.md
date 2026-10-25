# Minnan Village — prefab độc lập

**Phiên bản đích:** Unity 2022.3 LTS, Built-in Render Pipeline. Shader của gói này không dùng trực tiếp cho URP/HDRP.

## Sử dụng

1. Import `MinnanVillage_Complete.unitypackage` vào dự án Unity Built-in.
2. Mở `Assets/MinnanVillage/Scenes/MinnanVillage_Demo.unity` để xem scene mẫu; hoặc kéo `Assets/MinnanVillage/Prefabs/MinnanVillage_Complete.prefab` vào scene của bạn.
3. Nhấn Play để xem lửa nhấp nháy, khói bay lên và nước chuyển động.
4. Dùng `Wheelchair_Start_Anchor` làm điểm đặt người chơi. Các camera tham khảo trong prefab mặc định tắt, camera ở scene mẫu đã bật.
5. Nếu dự án đã có hệ thống ánh sáng, tắt nhánh `Daylight_Optional`. Component `MinnanDaylight` áp dụng skybox và ánh sáng môi trường khi bật; bỏ chọn `applySceneEnvironment` để giữ thiết lập của scene hiện tại.

## Những gì đã đóng gói

- Mô hình hiện có, vị trí nhà, ngõ, lò đốt, xe tải, kênh và lan can.
- Vật liệu/shader Unity cho gạch, ngói, đá, gỗ, kim loại và nước; dùng tọa độ không gian nên không phụ thuộc UV của mô hình cũ.
- Lửa mesh và than hồng có emission; script tạo nhấp nháy nhẹ và đèn ấm.
- Khói dùng Particle System nhẹ với texture mềm 64 × 64; các smoke card Blender được giữ nhưng tắt.
- Collider cho đường, tường, xe, vật cản chính và ranh giới kênh.
- Prefab hoàn chỉnh, scene mẫu, rig ánh sáng tùy chọn và camera góc nhìn ngồi.

## Giới hạn cần biết

- Vật liệu và hiệu ứng được tái tạo trong Unity, không phải bản bake giống từng pixel của Blender. Không có cam kết hai renderer cho kết quả tuyệt đối giống nhau.
- Đây là gói môi trường, chưa có bộ điều khiển xe lăn, input, UI hay logic nhiệm vụ.
- Điểm xuất phát nằm ngoài nhà. Bậc cửa nhà vẫn có hai cấp cao 150 mm, chưa phải lối vào không bậc.
- Lối cạnh xe tải giữ rộng 1,10 m. Kiểm tra thêm khi gắn controller có kích thước/skin width riêng.
- Không bật tự động Generate Colliders cho toàn bộ FBX: prefab đã có collider chủ đích để tránh collider lá cây, khói, mái ngói và chi tiết nhỏ.
- Không nên chạy lại menu xây gói trong dự án đang sử dụng prefab nếu không muốn tái tạo các asset đã tùy chỉnh. Mã trong thư mục Editor được kèm để tái lập/kiểm tra gói, không chạy trong bản game.
