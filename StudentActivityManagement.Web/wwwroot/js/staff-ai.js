// US-33 / US-34: AI tools for Phòng QL HSSV. Results are rendered with textContent only (never as HTML).
(() => {
    const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    if (!tokenInput) return;
    const token = tokenInput.value;

    function element(tag, className, text) {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined) node.textContent = text;
        return node;
    }

    async function post(url) {
        const response = await fetch(url, { method: 'POST', headers: { 'RequestVerificationToken': token } });
        if (response.redirected) {
            window.location.href = response.url;
            return null;
        }
        return response.json();
    }

    function busy(button, value, label) {
        button.disabled = value;
        if (value) {
            button.dataset.label = button.innerHTML;
            button.textContent = label;
        } else if (button.dataset.label) {
            button.innerHTML = button.dataset.label;
        }
    }

    // ---- Report commentary
    document.querySelectorAll('[data-ai-insights]').forEach(button => {
        const result = document.getElementById('ai-insights-result');
        button.addEventListener('click', async () => {
            busy(button, true, 'Đang phân tích…');
            result.hidden = false;
            result.replaceChildren(element('p', 'small text-muted mb-0', 'Gemini đang đọc số liệu của báo cáo…'));
            try {
                const data = await post(button.dataset.aiInsights);
                if (!data) return;
                result.replaceChildren();
                if (!data.success) {
                    result.append(element('div', 'alert alert-warning mb-0', data.message || 'Không tạo được nhận xét.'));
                    return;
                }
                const sections = [['Điểm nổi bật', data.highlights], ['Cần chú ý', data.concerns], ['Đề xuất', data.suggestions]];
                const grid = element('div', 'row g-3');
                sections.forEach(([title, items]) => {
                    const column = element('div', 'col-md-4');
                    column.append(element('h3', 'h6', title));
                    if (items.length === 0) {
                        column.append(element('p', 'small text-muted mb-0', 'Không có ý nào đủ căn cứ.'));
                    } else {
                        const list = element('ul', 'mb-0 ps-3');
                        items.forEach(item => list.append(element('li', '', item)));
                        column.append(list);
                    }
                    grid.append(column);
                });
                result.append(grid);
                const meta = `Kỳ ${data.periodName} · mô hình ${data.model}` +
                    (data.removedStatements > 0 ? ` · đã loại ${data.removedStatements} ý có số liệu không có trong báo cáo` : '') +
                    ' · AI có thể nhầm, hãy đối chiếu với các bảng bên dưới.';
                result.append(element('p', 'small text-muted mt-2 mb-0', meta));
            } catch {
                result.replaceChildren(element('div', 'alert alert-warning mb-0', 'Không kết nối được máy chủ. Vui lòng thử lại.'));
            } finally {
                busy(button, false);
            }
        });
    });

    // ---- Answer drafts
    document.querySelectorAll('[data-ai-draft]').forEach(button => {
        const target = document.getElementById(button.dataset.aiTarget);
        const note = document.getElementById(button.dataset.aiNote);
        button.addEventListener('click', async () => {
            if (target.value.trim() && !window.confirm('Thay nội dung đang soạn bằng bản nháp của AI?')) return;
            busy(button, true, 'Đang soạn nháp…');
            note.hidden = false;
            note.className = 'ai-note small mb-2 text-muted';
            note.textContent = 'Gemini đang soạn nháp dựa trên quy tắc của hệ thống…';
            try {
                const data = await post(button.dataset.aiDraft);
                if (!data) return;
                if (!data.success) {
                    note.className = 'ai-note small mb-2 text-danger';
                    note.textContent = data.message || 'Không soạn được bản nháp.';
                    return;
                }
                target.value = data.draft;
                target.focus();
                const parts = [`Bản nháp do AI gợi ý (độ chắc chắn: ${data.confidence}). Hãy đọc và sửa trước khi gửi.`];
                if (data.missingInformation.length > 0) parts.push(`Cần kiểm tra: ${data.missingInformation.join('; ')}.`);
                if (data.personalDataRedacted) parts.push('Thông tin cá nhân trong câu hỏi đã được ẩn trước khi gửi tới Gemini.');
                note.className = 'ai-note small mb-2 alert alert-info py-2';
                note.textContent = parts.join(' ');
            } catch {
                note.className = 'ai-note small mb-2 text-danger';
                note.textContent = 'Không kết nối được máy chủ. Vui lòng thử lại.';
            } finally {
                busy(button, false);
            }
        });
    });
})();
