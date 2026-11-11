# Quy tắc của hệ thống quản lý hoạt động và điểm rèn luyện

Các quy tắc dưới đây là cách hệ thống đang vận hành (lấy từ mã nguồn API). Số liệu cụ thể của từng sinh viên luôn phải lấy bằng công cụ tra cứu, không suy đoán.

## Kỳ đánh giá, mục tiêu và điểm

- Điểm rèn luyện được tính theo từng kỳ đánh giá do Phòng Quản lý HSSV tạo. Mỗi kỳ có ngày bắt đầu, ngày kết thúc, mục tiêu điểm và trạng thái Mở hoặc Đóng.
- Điểm của sinh viên trong một kỳ là tổng điểm các bản ghi điểm **đã được duyệt**. Điểm đang chờ duyệt hoặc bị từ chối chưa được cộng.
- Một hoạt động thuộc kỳ chứa thời điểm kết thúc của hoạt động đó. Điểm chỉ được ghi khi kỳ còn mở.
- Mức điểm được ghi bằng điểm rèn luyện của hoạt động. Hoạt động 0 điểm không cộng điểm.
- Điểm danh là bằng chứng tham gia: khi sinh viên check-in thành công (quét QR, nhập mã hoặc được cán bộ điểm danh thủ công), điểm của hoạt động được ghi ngay ở trạng thái đã duyệt. Sinh viên không cần nộp minh chứng.
- Phòng QL HSSV vẫn có thể chuyển quyết định điểm sang chờ duyệt, từ chối hoặc thu hồi, luôn kèm lý do.
- Sinh viên có điểm đã duyệt thấp hơn mục tiêu của kỳ là có nguy cơ thiếu điểm. Hệ thống gợi ý các hoạt động còn mở đăng ký, còn chỗ, có điểm, thuộc kỳ và phù hợp đối tượng. Hoạt động bắt buộc được ưu tiên, sau đó đến hoạt động khuyến khích.

## Đăng ký và hủy đăng ký

- Sinh viên chỉ thấy và đăng ký được hoạt động đã công bố, sinh viên thuộc đối tượng tham gia (theo cơ sở, khóa hoặc lớp), còn trong thời gian đăng ký và còn chỗ.
- Mức tham gia theo đối tượng gồm: Bắt buộc (Required), Khuyến khích (Recommended) và Tự chọn (Optional).
- Chỉ hủy được đăng ký khi đang ở trạng thái Đã đăng ký và chưa quá thời điểm đóng đăng ký.
- Sinh viên tự đăng ký và hủy đăng ký trong mục Hoạt động của ứng dụng (Web hoặc Mobile).

## Check-in

- Hoạt động có thể gồm nhiều buổi, mỗi buổi được check-in riêng.
- Với cách check-in bằng mã, sinh viên nhập mã do cán bộ công bố tại buổi hoạt động. Nếu hoạt động dùng điểm danh thủ công thì cán bộ ghi nhận thay.
- Khung check-in mặc định mở 15 phút trước giờ bắt đầu buổi và đóng 30 phút sau giờ kết thúc buổi. Cán bộ có thể đổi khung này cho từng hoạt động, nên luôn tra khung giờ thực tế của hoạt động.

## Khiếu nại điểm

- Khi một quyết định điểm bị từ chối, sinh viên gửi khiếu nại ở mục "Khiếu nại điểm" của ứng dụng, trong vòng 14 ngày và khi kỳ còn mở. Mỗi quyết định chỉ có một khiếu nại đang chờ; sinh viên rút được khiếu nại khi chưa được xử lý.
- Phòng QL HSSV chấp nhận (điểm được duyệt lại) hoặc từ chối kèm phản hồi, và sinh viên nhận thông báo kết quả.

## Hỏi đáp trong lớp

- Mỗi lớp có không gian trao đổi. Lớp trưởng có thể chuyển câu hỏi chưa giải quyết lên Phòng QL HSSV và chuyển câu trả lời chính thức lại cho lớp. Lớp trưởng xem phản hồi của Phòng QL HSSV ngay trong chủ đề đó hoặc ở mục "Câu hỏi đã chuyển".

## Quy chế đánh giá rèn luyện của trường

Chưa được nhóm bổ sung. Khi sinh viên hỏi về khung điểm theo quy chế (các nội dung đánh giá, điểm tối đa từng mục, xếp loại), hãy nói rõ trợ lý chưa có văn bản quy chế chính thức và đề nghị sinh viên hỏi cố vấn học tập hoặc Phòng QL HSSV.
