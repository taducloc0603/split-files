"""File Splitter - optimized for speed."""

import os
import shutil
import tempfile
import threading
import zipfile

SIZE_UNITS = {"KB": 1024, "MB": 1024 * 1024, "GB": 1024 * 1024 * 1024}

CHUNK_SIZE = 8 * 1024 * 1024  # 8 MB chunks


def _part_name(source_path: str, index: int) -> str:
    base, ext = os.path.splitext(os.path.basename(source_path))
    return f"{index}_{base}{ext}"


def split_by_size(source_path, max_bytes, has_header, encoding, progress_cb, output_dir):
    total_size = os.path.getsize(source_path)
    
    with open(source_path, "rb") as src:
        # Read header once
        if has_header:
            header_line = b""
            while True:
                byte = src.read(1)
                if not byte or byte == b"\n":
                    if byte:
                        header_line += byte
                    break
                header_line += byte
        else:
            header_line = b""
        
        header_bytes = len(header_line)
        if header_bytes >= max_bytes:
            raise ValueError("Dung lượng phần nhỏ hơn kích thước header. Tăng dung lượng lên.")
        
        file_index = 1
        current_bytes = 0
        out = None
        files_created = []
        bytes_read = header_bytes
        
        try:
            while True:
                chunk = src.read(CHUNK_SIZE)
                if not chunk:
                    break
                
                # Split chunk by newline
                lines = chunk.split(b"\n")
                
                for i, line in enumerate(lines):
                    if i < len(lines) - 1:
                        line += b"\n"  # Re-add newline except last (incomplete) line
                    
                    if not line:
                        continue
                    
                    line_bytes = len(line)
                    
                    # Open new file if needed
                    if out is None:
                        path = os.path.join(output_dir, _part_name(source_path, file_index))
                        out = open(path, "wb")
                        files_created.append(path)
                        current_bytes = 0
                        if has_header:
                            out.write(header_line)
                            current_bytes = header_bytes
                    
                    # Roll over if line would exceed cap (keep row intact)
                    has_data = current_bytes > header_bytes
                    if has_data and current_bytes + line_bytes > max_bytes:
                        out.close()
                        file_index += 1
                        path = os.path.join(output_dir, _part_name(source_path, file_index))
                        out = open(path, "wb")
                        files_created.append(path)
                        current_bytes = 0
                        if has_header:
                            out.write(header_line)
                            current_bytes = header_bytes
                    
                    out.write(line)
                    current_bytes += line_bytes
                    bytes_read += line_bytes
                
                if total_size:
                    progress_cb(min(100.0, bytes_read * 100 / total_size))
        finally:
            if out:
                out.close()
    
    progress_cb(100.0)
    return files_created


def split_by_rows(source_path, rows_per_file, has_header, encoding, progress_cb, output_dir):
    total_size = os.path.getsize(source_path)
    
    with open(source_path, "rb") as src:
        if has_header:
            header_line = b""
            while True:
                byte = src.read(1)
                if not byte or byte == b"\n":
                    if byte:
                        header_line += byte
                    break
                header_line += byte
        else:
            header_line = b""
        
        file_index = 1
        rows_in_current = 0
        out = None
        files_created = []
        bytes_read = len(header_line)
        
        try:
            while True:
                chunk = src.read(CHUNK_SIZE)
                if not chunk:
                    break
                
                lines = chunk.split(b"\n")
                for i, line in enumerate(lines):
                    if i < len(lines) - 1:
                        line += b"\n"
                    
                    if not line:
                        continue
                    
                    if out is None:
                        path = os.path.join(output_dir, _part_name(source_path, file_index))
                        out = open(path, "wb")
                        files_created.append(path)
                        rows_in_current = 0
                        if has_header:
                            out.write(header_line)
                    
                    out.write(line)
                    rows_in_current += 1
                    bytes_read += len(line)
                    
                    if rows_in_current >= rows_per_file:
                        out.close()
                        out = None
                        file_index += 1
                
                if total_size:
                    progress_cb(min(100.0, bytes_read * 100 / total_size))
        finally:
            if out:
                out.close()
    
    progress_cb(100.0)
    return files_created


def split_to_zip(source_path, splitter, progress_cb):
    source_dir = os.path.dirname(os.path.abspath(source_path))
    base, _ = os.path.splitext(os.path.basename(source_path))
    zip_path = os.path.join(source_dir, f"{base}_split.zip")

    tmp_dir = tempfile.mkdtemp(prefix="split_", dir=source_dir)
    try:
        part_paths = splitter(tmp_dir)
        progress_cb(100.0)
        with zipfile.ZipFile(zip_path, "w", compression=zipfile.ZIP_DEFLATED) as zf:
            for path in part_paths:
                zf.write(path, arcname=os.path.basename(path))
        return zip_path, [os.path.basename(p) for p in part_paths]
    finally:
        shutil.rmtree(tmp_dir, ignore_errors=True)


try:
    import tkinter as tk
    from tkinter import filedialog, messagebox, ttk
except ImportError:
    tk = None


class SplitterApp:
    def __init__(self, root):
        self.root = root
        root.title("File Splitter")
        root.geometry("560x360")
        root.resizable(False, False)

        self.source_path = tk.StringVar()
        self.mode = tk.StringVar(value="size")
        self.size_value = tk.StringVar(value="50")
        self.size_unit = tk.StringVar(value="MB")
        self.rows_value = tk.StringVar(value="100000")
        self.has_header = tk.BooleanVar(value=True)
        self.encoding = tk.StringVar(value="utf-8")
        self.progress = tk.DoubleVar(value=0.0)
        self.status = tk.StringVar(value="Sẵn sàng.")

        self._build_ui()
        self._update_mode_widgets()

    def _build_ui(self):
        pad = {"padx": 10, "pady": 6}

        file_frame = ttk.LabelFrame(self.root, text="File nguồn")
        file_frame.pack(fill="x", **pad)
        ttk.Entry(file_frame, textvariable=self.source_path).pack(
            side="left", fill="x", expand=True, padx=8, pady=8
        )
        ttk.Button(file_frame, text="Chọn file...", command=self._pick_file).pack(
            side="left", padx=6, pady=8
        )

        mode_frame = ttk.LabelFrame(self.root, text="Chế độ chia")
        mode_frame.pack(fill="x", **pad)
        ttk.Radiobutton(
            mode_frame, text="Theo dung lượng", value="size",
            variable=self.mode, command=self._update_mode_widgets,
        ).grid(row=0, column=0, sticky="w", padx=8, pady=4)
        ttk.Radiobutton(
            mode_frame, text="Theo số dòng", value="rows",
            variable=self.mode, command=self._update_mode_widgets,
        ).grid(row=0, column=1, sticky="w", padx=8, pady=4)

        self.size_entry = ttk.Entry(mode_frame, textvariable=self.size_value, width=12)
        self.size_entry.grid(row=1, column=0, sticky="w", padx=8, pady=4)
        self.size_unit_combo = ttk.Combobox(
            mode_frame, textvariable=self.size_unit,
            values=list(SIZE_UNITS.keys()), width=5, state="readonly",
        )
        self.size_unit_combo.grid(row=1, column=1, sticky="w", padx=0, pady=4)

        self.rows_entry = ttk.Entry(mode_frame, textvariable=self.rows_value, width=14)
        self.rows_entry.grid(row=1, column=2, sticky="w", padx=8, pady=4)
        ttk.Label(mode_frame, text="dòng / file").grid(
            row=1, column=3, sticky="w", padx=0, pady=4
        )

        opts_frame = ttk.LabelFrame(self.root, text="Tùy chọn")
        opts_frame.pack(fill="x", **pad)
        ttk.Checkbutton(
            opts_frame,
            text="File có dòng tiêu đề (lặp lại trong mỗi file con)",
            variable=self.has_header,
        ).grid(row=0, column=0, sticky="w", padx=8, pady=4)
        ttk.Label(opts_frame, text="Encoding:").grid(row=0, column=1, padx=(20, 4))
        ttk.Combobox(
            opts_frame, textvariable=self.encoding,
            values=["utf-8", "utf-8-sig", "cp1252", "latin-1"],
            width=10, state="readonly",
        ).grid(row=0, column=2, padx=4)

        progress_frame = ttk.Frame(self.root)
        progress_frame.pack(fill="x", **pad)
        ttk.Progressbar(
            progress_frame, variable=self.progress, maximum=100
        ).pack(fill="x", padx=8, pady=(4, 0))
        ttk.Label(progress_frame, textvariable=self.status).pack(
            anchor="w", padx=8, pady=(2, 4)
        )

        self.run_button = ttk.Button(self.root, text="Chia file", command=self._run)
        self.run_button.pack(pady=8)

    def _update_mode_widgets(self):
        if self.mode.get() == "size":
            self.size_entry.state(["!disabled"])
            self.size_unit_combo.state(["!disabled"])
            self.rows_entry.state(["disabled"])
        else:
            self.size_entry.state(["disabled"])
            self.size_unit_combo.state(["disabled"])
            self.rows_entry.state(["!disabled"])

    def _pick_file(self):
        path = filedialog.askopenfilename(
            title="Chọn file cần chia",
            filetypes=[("CSV / text", "*.csv *.txt *.tsv *.log"), ("Tất cả", "*.*")],
        )
        if path:
            self.source_path.set(path)

    def _set_progress(self, pct: float):
        self.progress.set(pct)
        self.status.set(f"Đang xử lý... {pct:.1f}%")
        self.root.update_idletasks()

    def _run(self):
        path = self.source_path.get().strip()
        if not path or not os.path.isfile(path):
            messagebox.showerror("Lỗi", "Vui lòng chọn file hợp lệ.")
            return

        mode = self.mode.get()
        has_header = self.has_header.get()

        try:
            if mode == "size":
                amount = float(self.size_value.get())
                if amount <= 0:
                    raise ValueError
                max_bytes = int(amount * SIZE_UNITS[self.size_unit.get()])
                splitter = lambda out_dir: split_by_size(
                    path, max_bytes, has_header, "utf-8", self._set_progress, out_dir
                )
            else:
                rows = int(self.rows_value.get())
                if rows <= 0:
                    raise ValueError
                splitter = lambda out_dir: split_by_rows(
                    path, rows, has_header, "utf-8", self._set_progress, out_dir
                )
        except ValueError:
            messagebox.showerror("Lỗi", "Giá trị nhập không hợp lệ.")
            return

        self.run_button.state(["disabled"])
        self.progress.set(0)
        self.status.set("Bắt đầu...")

        def task():
            try:
                self.status.set("Đang nén vào zip...")
                zip_path, parts = split_to_zip(path, splitter, self._set_progress)
                self.root.after(0, self._on_done, zip_path, parts, None)
            except Exception as exc:
                self.root.after(0, self._on_done, None, None, exc)

        threading.Thread(target=task, daemon=True).start()

    def _on_done(self, zip_path, parts, error):
        self.run_button.state(["!disabled"])
        if error is not None:
            self.status.set("Thất bại.")
            messagebox.showerror("Lỗi", str(error))
            return
        count = len(parts) if parts else 0
        self.status.set(f"Xong. Đã tạo zip với {count} file.")
        messagebox.showinfo(
            "Hoàn thành",
            f"Đã tạo zip chứa {count} file:\n{zip_path}",
        )


def main():
    if tk is None:
        raise RuntimeError(
            "Tkinter is not available. Install the standard Python distribution "
            "from python.org (Tkinter is bundled)."
        )
    root = tk.Tk()
    SplitterApp(root)
    root.mainloop()


if __name__ == "__main__":
    main()
