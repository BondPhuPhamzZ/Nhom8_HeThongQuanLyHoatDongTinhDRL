# Quy trình Git hằng ngày

## Bắt đầu task

Không code trực tiếp trên `main`.

```powershell
git switch main
git pull --rebase origin main
git switch -c feature/kiet-us-XX-ten-ngan
```

Mỗi branch chỉ xử lý một story hoặc một nhóm thay đổi liên quan chặt chẽ.

## Trước khi commit

```powershell
git status
git diff
dotnet build
```

Không commit các file `bin`, `obj`, database local, secret, log hoặc file tạm.

## Commit và push

```powershell
git add <cac-file-thuoc-task>
git commit -m "feat(us-XX): mo ta ngan gon"
git push -u origin feature/kiet-us-XX-ten-ngan
```

Ví dụ loại commit: `feat`, `fix`, `test`, `docs`, `refactor`, `chore`.

## Cập nhật main trước khi mở Pull Request

```powershell
git fetch origin
git rebase origin/main
dotnet build
git push --force-with-lease
```

Chỉ dùng `--force-with-lease` trên branch cá nhân sau khi rebase. Không force push `main`.

## Nội dung Pull Request

- Story ID và Acceptance Criteria đã làm.
- Endpoint hoặc model thay đổi.
- Migration mới và lệnh cập nhật database.
- Cách test và kết quả test.
- Breaking change hoặc việc Phú phải làm sau khi merge.

Phú review và merge. Kiệt không tự merge khi chưa được review.
