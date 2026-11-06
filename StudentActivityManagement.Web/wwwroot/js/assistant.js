// US-31: chat with the training-point assistant. The conversation lives only in this page; nothing is stored.
(() => {
    const root = document.querySelector('.assistant');
    if (!root) return;

    const log = document.getElementById('assistant-log');
    const form = document.getElementById('assistant-form');
    const input = document.getElementById('assistant-input');
    const send = document.getElementById('assistant-send');
    const suggestions = document.getElementById('assistant-suggestions');
    const reset = document.getElementById('assistant-reset');
    const token = root.querySelector('input[name="__RequestVerificationToken"]').value;
    const greeting = log.innerHTML;
    // The API accepts 12 messages; an odd count keeps the window starting with a question.
    const maxMessages = 11;
    let history = [];
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
        const messages = history.concat([{ role: 'user', content: question }]).slice(-maxMessages);

        try {
            const response = await fetch(root.dataset.chatUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
                body: JSON.stringify({ messages })
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

            history = messages.concat([{ role: 'assistant', content: data.reply }]);
            const row = addMessage('bot', data.reply);
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
    reset?.addEventListener('click', () => {
        if (busy) return;
        history = [];
        log.innerHTML = greeting;
        suggestions.hidden = false;
        input.focus();
    });
})();
