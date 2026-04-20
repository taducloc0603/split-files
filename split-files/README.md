# File Splitter (Windows)

Tool GUI để chia nhỏ file lớn (CSV, TXT, TSV, LOG...) theo **dung lượng** hoặc **số dòng**. Xử lý dạng stream nên chạy tốt với file vài trăm MB mà không tốn RAM, và luôn **cắt đúng trên biên dòng** để không phá vỡ bản ghi CSV.

## Yêu cầu

- Windows
- Python 3.8+ (tải tại https://www.python.org/downloads/windows/ — khi cài nhớ tick *Add Python to PATH*)
  - `tkinter` đi kèm sẵn trong bản Python chính thức, không cần `pip install` gì cả.

## Chạy

Cách 1 — double-click:

```
run.bat
```

Cách 2 — từ Command Prompt:

```
python file_splitter.py
```

## Hướng dẫn dùng

1. **Chọn file...** → trỏ tới file CSV 500MB trên Desktop.
2. Chọn **chế độ**:
   - *Theo dung lượng*: nhập số + đơn vị (KB/MB/GB). Mỗi file con sẽ xấp xỉ dung lượng đó; không bao giờ cắt giữa một dòng.
   - *Theo số dòng*: nhập số dòng trên mỗi file con.
3. **Tùy chọn**:
   - *File có dòng tiêu đề*: bật (mặc định) để lặp lại dòng header trong từng file con — đúng với CSV.
   - *Encoding*: mặc định `utf-8`; đổi sang `utf-8-sig`, `cp1252`, `latin-1` nếu cần.
4. Bấm **Chia file**. Kết quả được đóng gói thành 1 file zip `{tên_gốc}_split.zip` nằm cùng thư mục với file nguồn. Bên trong zip là các file con đặt tên `1_{tên_gốc}.csv`, `2_{tên_gốc}.csv`, ...

## Ghi chú

- Khi chọn theo dung lượng, tool đảm bảo toàn vẹn dữ liệu: đọc từng dòng và chỉ flush sang file mới khi thêm dòng tiếp theo sẽ vượt quá giới hạn.
- Với CSV có trường chứa ký tự xuống dòng trong dấu nháy kép, mỗi "dòng CSV" có thể trải dài nhiều dòng thật. Tool hiện coi mỗi newline là một bản ghi — phù hợp với CSV "phẳng" thông thường. Nếu cần xử lý CSV phức tạp, hãy mở issue.
