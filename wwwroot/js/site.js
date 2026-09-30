(() => {
  const form = document.querySelector('#chatForm');
  if (!form) return;
  const messages = document.querySelector('#chatMessages');
  const input = document.querySelector('#messageInput');
  const send = document.querySelector('#sendButton');
  const health = document.querySelector('#healthButton');
  const clear = document.querySelector('#clearButton');
  const memoryStrategy = document.querySelector('#memoryStrategy');
  const showThinking = document.querySelector('#showThinking');
  const stageTabs = document.querySelectorAll('.analysis-tab');
  const serverState = document.querySelector('#serverState');
  const toast = document.querySelector('#toast');
  const csrf = form.querySelector('input[name="__RequestVerificationToken"]').value;
  const memoryLabels = { None:'無記憶', Window:'滑動視窗', Buffer:'完整緩衝', Summary:'摘要記憶' };
  const thinkingPreferenceKey = 'ai-show-thinking';
  const legacyThinkingPreferenceKey = 'qwen-show-thinking';

  const savedThinkingPreference = window.localStorage.getItem(thinkingPreferenceKey)
    ?? window.localStorage.getItem(legacyThinkingPreferenceKey);
  showThinking.checked = savedThinkingPreference === null || savedThinkingPreference === 'true';
  document.documentElement.classList.toggle('hide-thinking', !showThinking.checked);

  const escapeHtml = value => { const node = document.createElement('div'); node.textContent = value; return node.innerHTML; };
  const showToast = message => {
    toast.textContent = message; toast.classList.add('show');
    window.clearTimeout(showToast.timer); showToast.timer = window.setTimeout(() => toast.classList.remove('show'), 4200);
  };
  const addMessage = (role, content, pending = false) => {
    document.querySelector('#emptyState')?.remove();
    const article = document.createElement('article');
    article.className = `message ${role}${pending ? ' pending' : ''}`;
    const sentAt = new Intl.DateTimeFormat('zh-TW', { hour:'2-digit', minute:'2-digit', hour12:false }).format(new Date());
    const receipt = role === 'user' ? `<span class="read-receipt"><b>已讀</b>${sentAt}</span>` : '';
    const label = role === 'user' ? '你' : role === 'thinking' ? 'AI 思考過程' : 'LLM';
    const firstLine = content.replace(/\r\n/g, '\n').split('\n')[0].trim();
    const bubbleContent = role === 'thinking'
      ? `<details class="thinking-disclosure"><summary>${escapeHtml(firstLine)}</summary><div>${escapeHtml(content)}</div></details>`
      : pending ? '<span class="typing"><i></i><i></i><i></i></span>' : escapeHtml(content);
    article.innerHTML = `<div class="message-label">${label}</div><div class="message-line">${receipt}<div class="bubble">${bubbleContent}</div></div>`;
    messages.appendChild(article); messages.scrollTo({ top: messages.scrollHeight, behavior: 'smooth' }); return article;
  };
  const resize = () => { input.style.height = 'auto'; input.style.height = `${Math.min(input.scrollHeight, 180)}px`; };
  const formatNs = ns => { if (!Number.isFinite(ns)) return '—'; const ms = ns / 1e6; return ms >= 1000 ? `${(ms / 1000).toFixed(ms >= 10000 ? 1 : 2)} 秒` : `${Math.round(ms)} 毫秒`; };
  const setMetric = (id, value) => { document.querySelector(`#${id}`).textContent = value; };
  const updateMetrics = (m, strategy, contextMessages, memory) => {
    const memoryLabel = memoryLabels[strategy] || strategy;
    setMetric('metricTotal', formatNs(m.totalDurationNanoseconds)); setMetric('metricRoundTrip', `${(m.roundTripMilliseconds / 1000).toFixed(2)} 秒`);
    setMetric('metricSpeed', m.tokensPerSecond ? m.tokensPerSecond.toFixed(1) : '—'); setMetric('metricResponseTokens', m.responseTokens.toLocaleString());
    setMetric('metricPromptTokens', `${m.promptTokens.toLocaleString()} 個輸入 · ${m.cachedPromptTokens.toLocaleString()} 個快取`);
    setMetric('metricLoad', formatNs(m.loadDurationNanoseconds)); setMetric('metricPrompt', formatNs(m.promptEvalDurationNanoseconds)); setMetric('metricEval', formatNs(m.evalDurationNanoseconds));
    const doneReason = { stop:'正常停止', length:'達長度上限' }[m.doneReason] || m.doneReason || '完成';
    setMetric('metricState', `已完成 · ${doneReason}`); setMetric('contextLabel', `${memoryLabel} · ${contextMessages} 則上下文訊息`);
    setMetric('metricMemory', memoryLabel);
    setMetric('metricMemoryDetail', memory?.summaryPresent
      ? `最近 ${memory.recentTurnsSent} 回合 · 摘要 ${memory.summaryCharacters} 字元${memory.summaryUpdated ? ' · 已更新' : ''}`
      : `最近 ${memory?.recentTurnsSent || 0} 回合 · 無摘要`);
    const total = Math.max(m.loadDurationNanoseconds + m.promptEvalDurationNanoseconds + m.evalDurationNanoseconds, 1);
    document.querySelector('#barLoad').style.width = `${m.loadDurationNanoseconds / total * 100}%`;
    document.querySelector('#barPrompt').style.width = `${m.promptEvalDurationNanoseconds / total * 100}%`;
    document.querySelector('#barEval').style.width = `${m.evalDurationNanoseconds / total * 100}%`;
  };

  form.addEventListener('submit', async event => {
    event.preventDefault(); const value = input.value.trim(); if (!value || send.disabled) return;
    addMessage('user', value); input.value = ''; resize(); send.disabled = true; stageTabs.forEach(tab => { tab.disabled = true; }); setMetric('metricState', 'AI 思考中…');
    const pending = addMessage('assistant', '', true);
    try {
      const response = await fetch('/chat/send', { method:'POST', headers:{ 'Content-Type':'application/json', 'RequestVerificationToken':csrf }, body:JSON.stringify({ message:value, stage:form.dataset.stage }) });
      const body = await response.json(); if (!response.ok) throw new Error(body.error || '聊天請求失敗。');
      if (body.message.thinking?.trim()) {
        const thinking = addMessage('thinking', body.message.thinking);
        messages.insertBefore(thinking, pending);
      }
      const answerBubble = pending.querySelector('.bubble');
      answerBubble.classList.add('markdown-body');
      answerBubble.innerHTML = body.renderedContent;
      pending.classList.remove('pending'); updateMetrics(body.metrics, body.memoryStrategy, body.contextMessages, body.memory);
    } catch (error) { pending.remove(); setMetric('metricState', '請求失敗'); showToast(error.message); }
    finally { send.disabled = false; stageTabs.forEach(tab => { tab.disabled = false; }); input.focus(); }
  });
  input.addEventListener('input', resize);
  showThinking.addEventListener('change', () => {
    document.documentElement.classList.toggle('hide-thinking', !showThinking.checked);
    window.localStorage.setItem(thinkingPreferenceKey, String(showThinking.checked));
  });
  input.addEventListener('keydown', event => { if (event.key === 'Enter' && !event.shiftKey) { event.preventDefault(); form.requestSubmit(); } });
  document.querySelectorAll('[data-prompt]').forEach(button => button.addEventListener('click', () => { input.value = button.dataset.prompt; resize(); input.focus(); }));
  stageTabs.forEach(tab => tab.addEventListener('click', async () => {
    if (tab.classList.contains('active')) return;
    stageTabs.forEach(item => { item.disabled = true; });
    try {
      const response = await fetch('/chat/stage', { method:'POST', headers:{ 'Content-Type':'application/json', 'RequestVerificationToken':csrf }, body:JSON.stringify({ stage:tab.dataset.stage }) });
      const body = await response.json(); if (!response.ok) throw new Error(body.error || '無法切換分析項目。');
      window.location.reload();
    } catch (error) {
      stageTabs.forEach(item => { item.disabled = false; });
      showToast(error.message);
    }
  }));
  memoryStrategy?.addEventListener('change', async () => {
    memoryStrategy.disabled = true;
    try {
      const response = await fetch('/chat/memory', { method:'POST', headers:{ 'Content-Type':'application/json', 'RequestVerificationToken':csrf }, body:JSON.stringify({ strategy:memoryStrategy.value }) });
      const body = await response.json(); if (!response.ok) throw new Error(body.error || '無法變更記憶模式。');
      window.location.reload();
    } catch (error) { memoryStrategy.disabled = false; showToast(error.message); }
  });
  health.addEventListener('click', async () => {
    health.disabled = true; serverState.className = 'server-state'; serverState.lastElementChild.textContent = '檢查中…';
    try {
      const response = await fetch('/chat/health'); const body = await response.json(); serverState.classList.add(body.online ? 'online' : 'offline');
      serverState.lastElementChild.textContent = body.online ? `連線正常 · ${body.latencyMilliseconds} 毫秒` : '無法連線'; if (!response.ok) showToast(body.message);
    } catch { serverState.classList.add('offline'); serverState.lastElementChild.textContent = '無法連線'; showToast('無法執行 Ollama 連線檢查。'); }
    finally { health.disabled = false; }
  });
  clear.addEventListener('click', async () => {
    const response = await fetch('/chat/clear', { method:'POST', headers:{ 'RequestVerificationToken':csrf } });
    if (response.ok) window.location.reload(); else showToast('無法清除對話。');
  });
  resize(); messages.scrollTop = messages.scrollHeight;
})();
