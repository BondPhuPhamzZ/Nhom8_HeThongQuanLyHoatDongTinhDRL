// Send only the new question. The API loads the student's saved conversation.
(() => {
    const root = document.querySelector('.assistant');
    if (!root) return;

    const log = document.getElementById('assistant-log');
    const form = document.getElementById('assistant-form');
    const input = document.getElementById('assistant-input');
    const send = document.getElementById('assistant-send');
    const suggestions = document.getElementById('assistant-suggestions');
    const token = root.querySelector('input[name="__RequestVerificationToken"]').value;
    let conversationId = Number(root.dataset.conversationId) || null;
    log.scrollTop = log.scrollHeight;
    let busy = false;

    function element(tag, className, text) {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined) node.textContent = text;
        return node;
    }

    function addMessage(role, text) {
        const row = element('div', `assistant-msg assistant-msg-${role === 'user' ? 'user' : 'bot'}`);
        row.append(element('div', 'assistant-bubble', text));
        log.append(row);
        log.scrollTop = log.scrollHeight;
        return row;
    }

    function activityCard(activity) {
        const card = element('a', 'card assistant-activity');
        card.href = activity.url;
        const head = element('div', 'd-flex justify-content-between gap-2');
        head.append(element('strong', '', activity.name), element('span', 'badge text-bg-primary align-self-start', `${activity.points} điểm`));
        const meta = [activity.campus, activity.start && `Bắt đầu ${activity.start}`,
            activity.registration ? activity.registration
                : activity.registrationClose && `${activity.isRegistrationOpen ? 'Đóng đăng ký' : 'Đã đóng đăng ký'} ${activity.registrationClose}`]
            .filter(Boolean).join(' · ');
        card.append(head, element('div', 'small text-muted', meta));
        return card;
    }

    function setBusy(value) {
        busy = value;
        send.disabled = value;
        input.disabled = value;
        suggestions.querySelectorAll('button').forEach(button => { button.disabled = value; });
    }

    async function ask(text) {
        const question = text.trim();
        if (busy || !question) return;
        setBusy(true);
        suggestions.hidden = true;
        addMessage('user', question);
        const pending = addMessage('bot', 'Đang tra cứu dữ liệu của bạn…');
        pending.classList.add('assistant-pending');

        try {
            const response = await fetch(root.dataset.chatUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
                body: JSON.stringify({ conversationId, message: question })
            });
            if (response.redirected) {
                // The session expired: the Web app redirects to the login page.
                window.location.href = response.url;
                return;
            }

            const data = await response.json();
            pending.remove();
            if (!data.success) {
                addMessage('bot', data.message || 'Có lỗi xảy ra. Vui lòng thử lại.').classList.add('assistant-error');
                return;
            }

            if (conversationId !== data.conversationId) {
                conversationId = data.conversationId;
                const url = new URL(window.location.href);
                url.searchParams.delete('newChat');
                url.searchParams.set('conversationId', String(conversationId));
                window.history.replaceState(null, '', url);
            }
            const remaining = document.getElementById('assistant-remaining');
            if (remaining) remaining.textContent = data.remainingToday;
            const warning = document.getElementById('assistant-token-warning');
            if (warning) warning.hidden = !(data.dailyTokenLimit > 0 && data.tokensUsedToday * 10 > data.dailyTokenLimit * 9);
            const row = addMessage('bot', data.reply);
            if (data.personalDataRedacted) {
                row.append(element('div', 'assistant-tools small text-muted',
                    'Mình đã ẩn thông tin cá nhân (họ tên, mã sinh viên, email, số điện thoại) trước khi gửi tới Gemini.'));
            }
            if (data.activities.length > 0) {
                const list = element('div', 'assistant-activities');
                data.activities.forEach(activity => list.append(activityCard(activity)));
                row.append(list);
            }
            if (data.toolsUsed.length > 0) {
                row.append(element('div', 'assistant-tools small text-muted', `Đã tra cứu: ${data.toolsUsed.join(', ')}`));
            }
            log.scrollTop = log.scrollHeight;
        } catch {
            pending.remove();
            addMessage('bot', 'Không kết nối được máy chủ. Vui lòng thử lại.').classList.add('assistant-error');
        } finally {
            setBusy(false);
            input.focus();
        }
    }

    form.addEventListener('submit', event => {
        event.preventDefault();
        const text = input.value;
        input.value = '';
        ask(text);
    });
    input.addEventListener('keydown', event => {
        if (event.key === 'Enter' && !event.shiftKey) {
            event.preventDefault();
            form.requestSubmit();
        }
    });
    suggestions.addEventListener('click', event => {
        const button = event.target.closest('button[data-question]');
        if (button) ask(button.dataset.question);
    });
})();
