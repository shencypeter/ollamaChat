(() => {
  const workspaceTabs = document.querySelectorAll('[data-workspace-tab]');
  const workspacePanels = document.querySelectorAll('[data-workspace-panel]');
  const reviewForm = document.querySelector('#reviewForm');
  const compareForm = document.querySelector('#compareForm');
  const chatForm = document.querySelector('#chatForm');
  if (!reviewForm || !compareForm || !chatForm) return;

  const messages = document.querySelector('#chatMessages');
  const chatInput = document.querySelector('#messageInput');
  const memoryStrategy = document.querySelector('#memoryStrategy');
  const showThinking = document.querySelector('#showThinking');
  const health = document.querySelector('#healthButton');
  const clear = document.querySelector('#clearButton');
  const serverState = document.querySelector('#serverState');
  const toast = document.querySelector('#toast');
  const memoryLabels = { None:'無記憶', Window:'滑動視窗', Buffer:'完整緩衝', Summary:'摘要記憶' };
  const thinkingPreferenceKey = 'ai-show-thinking';
  const savedThinkingPreference = window.localStorage.getItem(thinkingPreferenceKey)
    ?? window.localStorage.getItem('qwen-show-thinking');

  showThinking.checked = savedThinkingPreference === null || savedThinkingPreference === 'true';
  document.documentElement.classList.toggle('hide-thinking', !showThinking.checked);

  const csrf = form => form.querySelector('input[name="__RequestVerificationToken"]').value;
  const escapeHtml = value => { const node = document.createElement('div'); node.textContent = value; return node.innerHTML; };
  const showToast = message => {
    toast.textContent = message;
    toast.classList.add('show');
    window.clearTimeout(showToast.timer);
    showToast.timer = window.setTimeout(() => toast.classList.remove('show'), 4200);
  };
  const resizeField = field => {
    if (!field) return;
    field.style.height = 'auto';
    field.style.height = `${Math.min(field.scrollHeight, 260)}px`;
  };
  const setMetric = (id, value) => { document.querySelector(`#${id}`).textContent = value; };
  const formatNs = ns => {
    if (!Number.isFinite(ns)) return '—';
    const ms = ns / 1e6;
    return ms >= 1000 ? `${(ms / 1000).toFixed(ms >= 10000 ? 1 : 2)} 秒` : `${Math.round(ms)} 毫秒`;
  };
  const updateMetrics = (body, kind) => {
    const m = body.metrics;
    const isChat = kind === 'chat';
    const label = isChat ? (memoryLabels[body.memoryStrategy] || body.memoryStrategy) : '單次評閱';
    setMetric('metricTotal', formatNs(m.totalDurationNanoseconds));
    setMetric('metricRoundTrip', `${(m.roundTripMilliseconds / 1000).toFixed(2)} 秒`);
    setMetric('metricSpeed', m.tokensPerSecond ? m.tokensPerSecond.toFixed(1) : '—');
    setMetric('metricResponseTokens', m.responseTokens.toLocaleString());
    setMetric('metricPromptTokens', `${m.promptTokens.toLocaleString()} 個輸入 · ${m.cachedPromptTokens.toLocaleString()} 個快取`);
    setMetric('metricLoad', formatNs(m.loadDurationNanoseconds));
    setMetric('metricPrompt', formatNs(m.promptEvalDurationNanoseconds));
    setMetric('metricEval', formatNs(m.evalDurationNanoseconds));
    const doneReason = { stop:'正常停止', length:'達長度上限' }[m.doneReason] || m.doneReason || '完成';
    setMetric('metricState', `已完成 · ${doneReason}`);
    setMetric('contextLabel', `${label} · ${body.contextMessages} 則上下文訊息`);
    setMetric('metricMemory', label);
    setMetric('metricMemoryDetail', !isChat
      ? '未傳送任何先前作答或回饋'
      : body.memory?.summaryPresent
        ? `最近 ${body.memory.recentTurnsSent} 回合 · 摘要 ${body.memory.summaryCharacters} 字元${body.memory.summaryUpdated ? ' · 已更新' : ''}`
        : `最近 ${body.memory?.recentTurnsSent || 0} 回合 · 無摘要`);
    const total = Math.max(m.loadDurationNanoseconds + m.promptEvalDurationNanoseconds + m.evalDurationNanoseconds, 1);
    document.querySelector('#barLoad').style.width = `${m.loadDurationNanoseconds / total * 100}%`;
    document.querySelector('#barPrompt').style.width = `${m.promptEvalDurationNanoseconds / total * 100}%`;
    document.querySelector('#barEval').style.width = `${m.evalDurationNanoseconds / total * 100}%`;
  };
  const postChat = async (form, payload) => {
    const response = await fetch('/chat/send', {
      method:'POST',
      headers:{ 'Content-Type':'application/json', 'RequestVerificationToken':csrf(form) },
      body:JSON.stringify(payload)
    });
    const body = await response.json();
    if (!response.ok) throw new Error(body.error || '請求失敗。');
    return body;
  };
  const renderFeedback = (target, body, title) => {
    const thinking = body.message.thinking?.trim();
    const thinkingHtml = thinking
      ? `<details class="feedback-thinking"><summary>${escapeHtml(thinking.replace(/\r\n/g, '\n').split('\n')[0].trim())}</summary><div>${escapeHtml(thinking)}</div></details>`
      : '';
    target.classList.add('has-response');
    target.innerHTML = `${thinkingHtml}<p class="feedback-title">${title}</p><div class="markdown-body">${body.renderedContent}</div>`;
  };
  const setFormBusy = (form, busy) => {
    form.querySelectorAll('button,textarea,select').forEach(element => { element.disabled = busy; });
  };

  workspaceTabs.forEach(tab => tab.addEventListener('click', () => {
    const selected = tab.dataset.workspaceTab;
    workspaceTabs.forEach(item => {
      const active = item === tab;
      item.classList.toggle('active', active);
      item.setAttribute('aria-selected', String(active));
    });
    workspacePanels.forEach(panel => {
      const active = panel.dataset.workspacePanel === selected;
      panel.hidden = !active;
      panel.classList.toggle('active', active);
    });
    if (selected === 'chat') messages.scrollTop = messages.scrollHeight;
  }));

  reviewForm.addEventListener('submit', async event => {
    event.preventDefault();
    const input = document.querySelector('#reviewInput');
    const value = input.value.trim();
    if (!value) { showToast('請貼上要評閱的學生作答。'); input.focus(); return; }
    const target = document.querySelector('#reviewFeedback');
    setFormBusy(reviewForm, true);
    target.classList.remove('has-response');
    target.innerHTML = '<div class="feedback-empty"><strong>AI 正在評閱…</strong><span>正在檢查作答與所選階段的學習目標。</span></div>';
    setMetric('metricState', 'AI 思考中…');
    try {
      const body = await postChat(reviewForm, { message:value, stage:document.querySelector('#reviewStage').value, compare:false });
      renderFeedback(target, body, 'TA 評閱結果');
      updateMetrics(body, 'review');
    } catch (error) {
      target.innerHTML = '<div class="feedback-empty"><strong>評閱失敗</strong><span>請檢查 Ollama 連線後再試一次。</span></div>';
      setMetric('metricState', '請求失敗'); showToast(error.message);
    } finally { setFormBusy(reviewForm, false); }
  });

  compareForm.addEventListener('submit', async event => {
    event.preventDefault();
    const before = document.querySelector('#beforeInput');
    const after = document.querySelector('#afterInput');
    const beforeValue = before.value.trim();
    const afterValue = after.value.trim();
    if (!beforeValue || !afterValue) { showToast('請同時提供修改前與修改後版本。'); (!beforeValue ? before : after).focus(); return; }
    const target = document.querySelector('#compareFeedback');
    setFormBusy(compareForm, true);
    target.classList.remove('has-response');
    target.innerHTML = '<div class="feedback-empty"><strong>AI 正在比較…</strong><span>正在檢查修改帶來的改善與變化。</span></div>';
    setMetric('metricState', 'AI 思考中…');
    try {
      const body = await postChat(compareForm, { message:afterValue, beforeMessage:beforeValue, stage:document.querySelector('#compareStage').value, compare:true });
      renderFeedback(target, body, 'TA 版本比較結果');
      updateMetrics(body, 'compare');
    } catch (error) {
      target.innerHTML = '<div class="feedback-empty"><strong>比較失敗</strong><span>請檢查 Ollama 連線後再試一次。</span></div>';
      setMetric('metricState', '請求失敗'); showToast(error.message);
    } finally { setFormBusy(compareForm, false); }
  });

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
    messages.appendChild(article);
    messages.scrollTo({ top:messages.scrollHeight, behavior:'smooth' });
    return article;
  };

  chatForm.addEventListener('submit', async event => {
    event.preventDefault();
    const value = chatInput.value.trim();
    const send = document.querySelector('#sendButton');
    if (!value || send.disabled) return;
    addMessage('user', value);
    chatInput.value = ''; resizeField(chatInput); send.disabled = true; memoryStrategy.disabled = true;
    setMetric('metricState', 'AI 思考中…');
    const pending = addMessage('assistant', '', true);
    try {
      const body = await postChat(chatForm, { message:value, stage:'generalChat', compare:false });
      if (body.message.thinking?.trim()) {
        const thinking = addMessage('thinking', body.message.thinking);
        messages.insertBefore(thinking, pending);
      }
      const bubble = pending.querySelector('.bubble');
      bubble.classList.add('markdown-body');
      bubble.innerHTML = body.renderedContent;
      pending.classList.remove('pending');
      updateMetrics(body, 'chat');
    } catch (error) {
      pending.remove(); setMetric('metricState', '請求失敗'); showToast(error.message);
    } finally { send.disabled = false; memoryStrategy.disabled = false; chatInput.focus(); }
  });

  chatInput.addEventListener('input', () => resizeField(chatInput));
  chatInput.addEventListener('keydown', event => { if (event.key === 'Enter' && !event.shiftKey) { event.preventDefault(); chatForm.requestSubmit(); } });
  showThinking.addEventListener('change', () => {
    document.documentElement.classList.toggle('hide-thinking', !showThinking.checked);
    window.localStorage.setItem(thinkingPreferenceKey, String(showThinking.checked));
  });
  memoryStrategy.addEventListener('change', async () => {
    memoryStrategy.disabled = true;
    try {
      const response = await fetch('/chat/memory', { method:'POST', headers:{ 'Content-Type':'application/json', 'RequestVerificationToken':csrf(chatForm) }, body:JSON.stringify({ strategy:memoryStrategy.value }) });
      const body = await response.json();
      if (!response.ok) throw new Error(body.error || '無法變更記憶模式。');
      showToast(`一般對話已切換為${memoryLabels[body.strategy] || body.strategy}。`);
    } catch (error) { showToast(error.message); }
    finally { memoryStrategy.disabled = false; }
  });
  health.addEventListener('click', async () => {
    health.disabled = true; serverState.className = 'server-state'; serverState.lastElementChild.textContent = '檢查中…';
    try {
      const response = await fetch('/chat/health'); const body = await response.json();
      serverState.classList.add(body.online ? 'online' : 'offline');
      serverState.lastElementChild.textContent = body.online ? `連線正常 · ${body.latencyMilliseconds} 毫秒` : '無法連線';
      if (!response.ok) showToast(body.message);
    } catch { serverState.classList.add('offline'); serverState.lastElementChild.textContent = '無法連線'; showToast('無法執行 Ollama 連線檢查。'); }
    finally { health.disabled = false; }
  });
  clear.addEventListener('click', async () => {
    const response = await fetch('/chat/clear', { method:'POST', headers:{ 'RequestVerificationToken':csrf(chatForm) } });
    if (response.ok) window.location.reload(); else showToast('無法清除一般對話。');
  });

  resizeField(chatInput);
})();
