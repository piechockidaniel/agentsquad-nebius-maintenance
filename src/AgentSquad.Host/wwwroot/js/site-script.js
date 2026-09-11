const bySelector = selector => document.querySelector(selector);
const access = bySelector('#access'),
    refreshButton = bySelector('#refresh'),
    continueButton = bySelector('#continue'),
    runButton = bySelector('#run'),
    runPanel = bySelector('#runPanel'),
    runPlaceholder = bySelector('#runPlaceholder'),
    runStatus = bySelector('#runStatus'),
    newRun = bySelector('#newRun'),
    activity = bySelector('#activity'),
    activityTitle = bySelector('#activityTitle'),
    activityDetail = bySelector('#activityDetail'),
    toast = bySelector('#toast'),
    toastTitle = bySelector('#toastTitle'),
    toastDetail = bySelector('#toastDetail'),
    toastMark = bySelector('#toastMark'),
    runButtons = [...document.querySelectorAll('.path-run')];
const preflightProbePolicy = Object.freeze({
    maxAttempts: 5,
    requestTimeoutMs: 12000,
    retryDelaysMs: [1000, 2000, 4000, 8000],
    cooldownMs: 30000
});
let selectedScenario = 'cache-repair',
    preflightReady = false,
    pollingId = null,
    activeStep = 1,
    furthestStep = 1,
    toastTimer = null,
    preflightInProgress = false,
    preflightCircuitUntil = 0,
    preflightCircuitTimer = null,
    manualCheckDefinitions = null,
    manualChecksLoading = null,
    manualChecksError = null,
    lastRunNotification = null;
access.value = sessionStorage.getItem('agentsquad-access-token') || '';
access.addEventListener('input', () => sessionStorage.setItem('agentsquad-access-token', access.value));

function escapeHtml(value) {
    return String(value ?? '').replace(/[&<>'"]/g, char => ({
        '&': '&amp;',
        '<': '&lt;',
        '>': '&gt;',
        "'": '&#39;',
        '"': '&quot;'
    })[char]);
}

function safeUrl(value) {
    try {
        const url = new URL(value);
        return url.protocol === 'https:' ? url.href : null;
    } catch {
        return null;
    }
}

function text(value, fallback = 'Not recorded') {
    return value == null || value === '' ? fallback : String(value);
}

function findStep(run, names) {
    return (run.toolSteps || []).find(step => names.includes(step.name));
}

function allSteps(run, names) {
    return (run.toolSteps || []).filter(step => names.includes(step.name));
}

function parseOutput(step) {
    try {
        return step?.output ? JSON.parse(step.output) : null;
    } catch {
        return null;
    }
}

function commandOutput(step) {
    const parsed = parseOutput(step),
        result = parsed?.result || parsed,
        stdout = result?.stdout?.value || result?.stdout || '',
        stderr = result?.stderr?.value || result?.stderr || '';
    return [stdout, stderr].filter(Boolean).join('\n').trim() || step?.output || '';
}

function terminal(value) {
    return ['Remediated', 'Blocked', 'Failed'].includes(value);
}

function outcomeClass(value) {
    return value === 'Remediated' ? 'remediated' : value === 'Blocked' ? 'blocked' : value === 'Failed' ? 'failed' :
        value === 'Queued' ? 'queued' : 'active';
}

function resultLabel(step, preferred) {
    const value = commandOutput(step);
    if (!value) return preferred || 'Awaiting result';
    if (/no vulnerable packages/i.test(value)) return 'No vulnerable packages';
    if (/vulnerable packages/i.test(value)) return 'Vulnerability detected';
    const passed = value.match(/Passed!\s*-\s*Failed:\s*(\d+),\s*Passed:\s*(\d+)/i) || value.match(/Passed:\s*(\d+)/i);
    return passed ? `Tests passed${passed[2] ? ` · ${passed[2]} passed` : ` · ${passed[1]} passed`}` : preferred ||
        'Completed';
}

function showActivity(title, detail) {
    activityTitle.textContent = title;
    activityDetail.textContent = detail;
    setRunButtonsDisabled(true);
    activity.classList.remove('hidden');
}

function hideActivity() {
    activity.classList.add('hidden');
    setRunButtonsDisabled(false);
}

function showToast(kind, title, detail) {
    window.clearTimeout(toastTimer);
    toast.className = `toast ${kind}`;
    toastTitle.textContent = title;
    toastDetail.textContent = detail;
    toastMark.textContent = kind === 'success' ? '✓' : '!';
    toastTimer = window.setTimeout(() => toast.classList.add('hidden'), 6000);
}

function setRunButtonsDisabled(disabled) {
    runButtons.forEach(button => {
        button.disabled = disabled;
    });
}

function updateIndicators() {
    [1, 2, 3].forEach(index => {
        const indicator = bySelector(`#indicator${index}`);
        indicator.classList.toggle('active', index === activeStep);
        indicator.classList.toggle('done', index < activeStep);
        indicator.disabled = index > furthestStep;
        indicator.setAttribute('aria-selected', String(index === activeStep));
    });
}

function goToStep(number) {
    if (number > furthestStep) return;
    activeStep = number;
    bySelector('#step1').classList.remove('hidden');
    bySelector('#step2').classList.toggle('hidden', number < 2);
    bySelector('#step3').classList.toggle('hidden', number < 3);
    updateIndicators();
    bySelector(`#step${number}`).scrollIntoView({
        block: 'start',
        behavior: 'smooth'
    });
}

function setPreflightCard(prefix, tone, title, detail) {
    const state = bySelector(`#${prefix}State`);
    state.className = `state ${tone}`;
    state.textContent = title;
    bySelector(`#${prefix}Detail`).textContent = detail;
}

function setBrandBadge(provider, tone, state, detail) {
    const badge = bySelector(`#badge${provider}`),
        stateText = bySelector(`#badge${provider}State`);
    badge.className = `tag brand-tag ${tone}`;
    badge.title = detail || state;
    badge.setAttribute('aria-label', `${provider}: ${state}. ${detail || ''}`);
    stateText.textContent = state;
}

function setAllBrandBadges(tone, state, detail) {
    ['Nebius', 'Nvidia', 'Tavily'].forEach(provider => setBrandBadge(provider, tone, state, detail));
}

function detailIndicatesReady(detail) {
    return !/unavailable|not configured|missing|failed|error/i.test(detail || '') && /exposes|available|connected/i
        .test(detail || '');
}

function wait(milliseconds) {
    return new Promise(resolve => window.setTimeout(resolve, milliseconds));
}

function formatSeconds(milliseconds) {
    return `${Math.max(1, Math.ceil(milliseconds / 1000))}s`;
}

function isRetryablePreflightResponse(preflight) {
    return !
        /disabled|not configured|missing sandbox tools|missing .*?(?:token|key)|not available to this Token Factory key/i
            .test([preflight.modelDetail, preflight.sandboxDetail].join(' '));
}

function isRetryablePreflightError(error) {
    return ![400, 401, 403, 404].includes(error?.status);
}

function setProbeProgress(attempt, detail) {
    const progress = `Probe ${attempt} of ${preflightProbePolicy.maxAttempts}`;
    bySelector('#preflightState').textContent = progress;
    bySelector('#preflightNotice').classList.remove('good');
    bySelector('#preflightNotice').textContent = `${progress}: ${detail}`;
    setAllBrandBadges('warming', progress, detail);
    showActivity('Checking platform readiness', `${progress} — ${detail}`);
}

function clearPreflightCircuit() {
    window.clearInterval(preflightCircuitTimer);
    preflightCircuitTimer = null;
    preflightCircuitUntil = 0;
}

function renderPreflightCircuit(reason) {
    const remaining = preflightCircuitUntil - Date.now();
    if (remaining <= 0) {
        clearPreflightCircuit();
        refreshButton.disabled = false;
        refreshButton.textContent = 'Retry connection';
        bySelector('#preflightState').textContent = 'Retry available';
        bySelector('#preflightNotice').textContent =
            'Automatic probes have stopped. You can retry the connection when ready.';
        return;
    }
    const waitText = formatSeconds(remaining);
    refreshButton.disabled = true;
    refreshButton.textContent = `Retry in ${waitText}`;
    bySelector('#preflightState').textContent = `Paused · ${waitText}`;
    bySelector('#preflightNotice').textContent =
        `Connection checks paused for ${waitText} after five unsuccessful probes. ${reason}`;
}

function openPreflightCircuit(reason) {
    preflightCircuitUntil = Date.now() + preflightProbePolicy.cooldownMs;
    window.clearInterval(preflightCircuitTimer);
    renderPreflightCircuit(reason);
    preflightCircuitTimer = window.setInterval(() => renderPreflightCircuit(reason), 1000);
}

function applyPreflight(preflight) {
    const modelReady = detailIndicatesReady(preflight.modelDetail),
        sandboxReady = detailIndicatesReady(preflight.sandboxDetail),
        tavilyReady = Boolean(preflight.tavilyConfigured);
    setPreflightCard('model', modelReady ? 'good' : 'error', modelReady ? (preflight.modelId || 'Available') :
        'Unavailable', preflight.modelDetail);
    setPreflightCard('sandbox', sandboxReady ? 'good' : 'error', sandboxReady ? 'Connected' : 'Needs attention',
        preflight.sandboxDetail);
    setPreflightCard('tavily', tavilyReady ? 'good' : 'warn', tavilyReady ? 'Configured' : 'Optional off', preflight
        .tavilyDetail);
    setBrandBadge('Nvidia', modelReady ? 'ok' : 'error', modelReady ? 'Available' : 'Unavailable', preflight
        .modelDetail);
    setBrandBadge('Nebius', sandboxReady ? 'ok' : 'error', sandboxReady ? 'Connected' : 'Needs attention', preflight
        .sandboxDetail);
    setBrandBadge('Tavily', tavilyReady ? 'ok' : 'warn', tavilyReady ? 'Configured' : 'Optional off', preflight
        .tavilyDetail);
    bySelector('#sandboxTools').innerHTML = (preflight.sandboxTools || []).slice(0, 4).map(name =>
        `<span class="pill">${escapeHtml(name)}</span>`).join('');
}

function markPreflightUnavailable(message) {
    setPreflightCard('model', 'error', 'Unreachable', message);
    setPreflightCard('sandbox', 'error', 'Unreachable', message);
    setPreflightCard('tavily', 'error', 'Not checked', 'The preflight request did not complete.');
    setAllBrandBadges('error', 'Probe failed', message);
}
async function getPreflight() {
    const controller = new AbortController(),
        timeout = window.setTimeout(() => controller.abort(), preflightProbePolicy.requestTimeoutMs);
    try {
        return await api('/maintenance/preflight', 'GET', controller.signal);
    } catch (error) {
        if (controller.signal.aborted) {
            const timedOut = new Error(
                `The readiness probe timed out after ${formatSeconds(preflightProbePolicy.requestTimeoutMs)}.`);
            timedOut.status = 408;
            throw timedOut;
        }
        throw error;
    } finally {
        window.clearTimeout(timeout);
    }
}
async function api(path, method = 'GET', signal) {
    const headers = {
        'Content-Type': 'application/json'
    };
    if (access.value) headers['X-AgentSquad-Access-Token'] = access.value;
    const response = await fetch(path, {
        method,
        headers,
        signal
    });
    if (!response.ok) {
        const body = await response.json().catch(() => ({})),
            error = new Error(body.error || `Request failed (${response.status})`);
        error.status = response.status;
        throw error;
    }
    return response.json();
}
async function ensureManualCheckDefinitions() {
    if (manualCheckDefinitions || manualChecksError) return;
    if (!manualChecksLoading) {
        manualChecksLoading = api('/maintenance/manual-checks').then(checks => {
            manualCheckDefinitions = checks;
        }).catch(error => {
            manualChecksError = error.message;
        }).finally(() => {
            manualChecksLoading = null;
        });
    }
    await manualChecksLoading;
}

function activeManualCheck(run) {
    return (run.manualChecks || []).find(check => ['Queued', 'Running'].includes(check.status)) || null;
}

function manualCheckOutcomeClass(status) {
    return status === 'Passed' ? 'passed' : status === 'Failed' ? 'failed' : 'active';
}

function renderManualCheckPanel(run) {
    if (run.status !== 'Remediated' || !run.sandboxSnapshotId)
        return '<section class="manual-check-panel unavailable"><h2>Manual Sandbox checks</h2><p>A manual check becomes available only after a repair produces a verified Sandbox snapshot.</p></section>';
    if (!manualCheckDefinitions)
        return `<section class="manual-check-panel unavailable"><h2>Manual Sandbox checks</h2><p>${escapeHtml(manualChecksError || 'Loading the fixed check catalog…')}</p></section>`;
    const active = activeManualCheck(run);
    const previousChecks = run.manualChecks || [];
    const cards = manualCheckDefinitions.map(definition => {
        const latest = [...previousChecks].reverse().find(check => check.checkId === definition.id);
        const status = latest ?
            `<span class="outcome ${manualCheckOutcomeClass(latest.status)}">${escapeHtml(latest.status)}</span>` :
            '';
        const evidence = latest?.output ?
            `<details><summary>View captured response</summary><pre>${escapeHtml(latest.output)}</pre></details>` :
            latest?.error ? `<p class="manual-check-error">${escapeHtml(latest.error)}</p>` : '';
        const disabled = active ? ' disabled' : '';
        return `<article class="manual-check-card"><div class="manual-check-card-top"><h3>${escapeHtml(definition.title)}</h3>${status}</div><p>${escapeHtml(definition.description)}</p><p class="expected">Expected · ${escapeHtml(definition.expectedResult)}</p>${evidence}<button class="secondary manual-check-button" type="button" data-manual-check="${escapeHtml(definition.id)}" data-run-id="${escapeHtml(run.id)}"${disabled}>Run in Sandbox</button></article>`;
    }).join('');
    return `<section class="manual-check-panel"><h2>Try the repaired snapshot</h2><p>These fixed checks start the patched app only inside a fresh, disposable Sandbox and call its private loopback API. No preview URL is exposed.</p><div class="manual-check-grid">${cards}</div></section>`;
}

function bindManualCheckButtons() {
    runPanel.querySelectorAll('.manual-check-button').forEach(button => button.addEventListener('click', () =>
        startManualCheck(button.dataset.runId, button.dataset.manualCheck)));
}
async function startManualCheck(runId, checkId) {
    if (pollingId) return;
    runPanel.querySelectorAll('.manual-check-button').forEach(button => button.disabled = true);
    showActivity('Manual Sandbox check in progress',
        'Starting the repaired snapshot in a fresh, private Sandbox session.');
    runStatus.textContent = 'Running the selected manual check inside the repaired Sandbox snapshot…';
    try {
        await api(`/maintenance/runs/${encodeURIComponent(runId)}/manual-checks/${encodeURIComponent(checkId)}`,
            'POST');
        await loadRun(runId);
    } catch (error) {
        hideActivity();
        runPanel.querySelectorAll('.manual-check-button').forEach(button => button.disabled = false);
        runStatus.textContent = `Manual check needs attention: ${error.message}`;
        showToast('error', 'Manual check did not start', error.message);
    }
}
async function refreshPreflight() {
    if (preflightInProgress) return;
    if (preflightCircuitUntil > Date.now()) {
        renderPreflightCircuit('No more requests are sent during the cooldown.');
        return;
    }
    clearPreflightCircuit();
    preflightInProgress = true;
    refreshButton.disabled = true;
    refreshButton.textContent = 'Checking…';
    continueButton.classList.add('hidden');
    preflightReady = false;
    furthestStep = 1;
    updateIndicators();
    let lastError = 'The platform did not become ready.';
    let retryable = true;
    try {
        for (let attempt = 1; attempt <= preflightProbePolicy.maxAttempts; attempt += 1) {
            setProbeProgress(attempt, 'contacting Token Factory, the Nebius Sandbox, and Tavily configuration.');
            try {
                const preflight = await getPreflight();
                applyPreflight(preflight);
                if (preflight.ready) {
                    preflightReady = true;
                    furthestStep = 2;
                    updateIndicators();
                    bySelector('#preflightState').textContent = 'Ready';
                    bySelector('#preflightNotice').classList.add('good');
                    bySelector('#preflightNotice').textContent =
                        `Platform readiness confirmed on probe ${attempt}. Opening the controlled maintenance paths.`;
                    refreshButton.textContent = 'Check again';
                    showToast('success', 'Platform ready', 'Model and Nebius Sandbox connectivity are confirmed.');
                    goToStep(2);
                    return;
                }
                lastError =
                    'Preflight must be ready before a maintenance path can be selected. Review the status cards above.';
                retryable = isRetryablePreflightResponse(preflight);
            } catch (error) {
                lastError = error.message;
                retryable = isRetryablePreflightError(error);
                markPreflightUnavailable(lastError);
            }
            if (!retryable || attempt === preflightProbePolicy.maxAttempts) {
                furthestStep = 1;
                updateIndicators();
                continueButton.classList.add('hidden');
                bySelector('#preflightState').textContent = retryable ? 'Connection paused' : 'Needs attention';
                bySelector('#preflightNotice').textContent = retryable ?
                    `Five probes did not establish readiness. ${lastError}` : lastError;
                showToast('error', retryable ? 'Connection paused' : 'Preflight needs attention', retryable ?
                    'Automatic probes stopped briefly; review the provider badges and retry after the cooldown.' :
                    lastError);
                if (retryable) openPreflightCircuit(lastError);
                return;
            }
            const delay = preflightProbePolicy.retryDelaysMs[attempt - 1];
            const nextProbe = attempt + 1;
            const retryDetail = `No connection yet. Retrying in ${formatSeconds(delay)}.`;
            bySelector('#preflightState').textContent =
                `Retrying · ${nextProbe} of ${preflightProbePolicy.maxAttempts}`;
            bySelector('#preflightNotice').textContent =
                `${retryDetail} Probe ${nextProbe} of ${preflightProbePolicy.maxAttempts} will start automatically.`;
            setAllBrandBadges('warming', `Retry ${nextProbe} of ${preflightProbePolicy.maxAttempts}`, retryDetail);
            showActivity('Checking platform readiness',
                `Probe ${attempt} did not finish. Retrying in ${formatSeconds(delay)}.`);
            await wait(delay);
        }
    } finally {
        preflightInProgress = false;
        hideActivity();
        if (preflightCircuitUntil <= Date.now()) {
            refreshButton.disabled = false;
            if (refreshButton.textContent === 'Checking…') refreshButton.textContent = 'Check platform readiness';
        }
    }
}

function activeStage(run) {
    const groups = [
        ['read_manifest', 'enforce_patch_policy', 'query_nuget_and_ghsa', 'confirm_advisory'],
        ['tavily_security_research'],
        ['nvidia_risk_summary'],
        ['resolve_base_image', 'prepare_fixture', 'sync_fixture', 'attach_fixture', 'baseline_scan'],
        ['baseline_tests', 'run_tests', 'post_patch_scan']
    ];
    const next = groups.findIndex(group => !allSteps(run, group).some(step => ['ok', 'failed', 'blocked', 'unavailable']
        .includes(step.status)));
    return next < 0 ? groups.length - 1 : next;
}

function stageState(run, names, index) {
    const steps = allSteps(run, names);
    if (steps.some(step => step.status === 'failed')) return 'failed';
    if (steps.some(step => ['blocked', 'unavailable'].includes(step.status))) return 'skipped';
    if (steps.some(step => step.status === 'ok')) return 'good';
    if (!terminal(run.status) && index === activeStage(run)) return 'active';
    if (terminal(run.status) && index > activeStage(run)) return 'skipped';
    return '';
}

function renderTimeline(run) {
    const stages = [
        ['Detect', ['read_manifest', 'enforce_patch_policy', 'query_nuget_and_ghsa', 'confirm_advisory'],
            'Manifest and advisory'
        ],
        ['Research', ['tavily_security_research'], 'Bounded security sources'],
        ['Explain', ['nvidia_risk_summary'], 'Evidence-grounded summary'],
        ['Sandbox', ['resolve_base_image', 'prepare_fixture', 'sync_fixture', 'attach_fixture', 'baseline_scan'],
            'Isolated patch staging'
        ],
        ['Verify', ['baseline_tests', 'run_tests', 'post_patch_scan'], 'Tests and clean scan']
    ];
    return stages.map(([title, names, detail], index) =>
        `<div class="timeline-step ${stageState(run, names, index)}"><strong>${title}</strong><span>${detail}</span></div>`
    ).join('');
}

function renderTavily(run) {
    const step = findStep(run, ['tavily_security_research']),
        research = parseOutput(step);
    if (!step)
        return '<article class="evidence-card"><div class="label">Tavily research</div><h3>Not reached</h3><p class="muted">This scenario stopped before supplementary research.</p></article>';
    const sources = research?.Sources || research?.sources || [],
        credits = research?.CreditsUsed ?? research?.creditsUsed,
        requestId = research?.RequestId ?? research?.requestId;
    const sourceMarkup = sources.length ?
        `<div class="source-list">${sources.map(source => { const url = safeUrl(source.Url ?? source.url), title = escapeHtml(source.Title ?? source.title ?? 'Security source'), host = url ? escapeHtml(new URL(url).hostname) : 'Untrusted source omitted'; return url ? `<a class="source" href="${escapeHtml(url)}" target="_blank" rel="noreferrer noopener"><strong>${title}</strong><small>${host}</small></a>` : `<span class="source"><strong>${title}</strong><small>${host}</small></span>`; }).join('')}</div>` :
        `<p class="muted">${escapeHtml(step.detail)}</p>`;
    return `<article class="evidence-card"><div class="label">Tavily research</div><h3>${step.status === 'ok' ? 'Supplementary sources captured' : 'Research unavailable'}</h3>${sourceMarkup}<p class="hint">Request: ${escapeHtml(text(requestId, 'not returned'))} · Credits: ${credits == null ? 'not returned' : escapeHtml(credits)}</p></article>`;
}

function renderCommandCard(label, step, fallback) {
    if (!step)
        return `<article class="evidence-card"><div class="label">${label}</div><h3>Not reached</h3><p class="muted">${fallback}</p></article>`;
    const output = commandOutput(step);
    return `<article class="evidence-card"><div class="label">${label}</div><h3>${escapeHtml(resultLabel(step, step.status === 'ok' ? 'Completed' : 'Failed'))}</h3><p class="muted">${escapeHtml(step.detail)}</p>${output ? `<details><summary>View command evidence</summary><pre>${escapeHtml(output)}</pre></details>` : ''}</article>`;
}

function renderTrace(run) {
    return (run.toolSteps || []).map(step =>
        `<article class="trace-item"><div class="trace-row"><span class="trace-name">${escapeHtml(step.name)}</span><span class="pill">${escapeHtml(step.status)}</span></div><p class="trace-detail">${escapeHtml(step.detail)} · ${escapeHtml(new Date(step.occurredAt).toLocaleTimeString())}</p>${step.output ? `<details><summary>View captured output</summary><pre>${escapeHtml(step.output)}</pre></details>` : ''}</article>`
    ).join('') || '<p class="muted">No tool activity has been recorded yet.</p>';
}

function renderRun(run) {
    runPlaceholder.classList.add('hidden');
    runPanel.classList.remove('hidden');
    const diff = findStep(run, ['manifest_diff']),
        summary = run.summary || run.error || 'The run is waiting for its first evidence step.',
        baseline = findStep(run, ['baseline_test', 'baseline_tests']),
        patched = findStep(run, ['run_tests', 'patched_test', 'patched_tests', 'test_patch']),
        scan = findStep(run, ['post_patch_scan', 'postpatch_scan']),
        sandbox = findStep(run, ['resolve_base_image', 'prepare_fixture', 'sync_fixture', 'attach_fixture']),
        advisoryUrl = run.advisoryId ? `https://github.com/advisories/${encodeURIComponent(run.advisoryId)}` : null;
    runPanel.innerHTML =
        `<div class="run-top"><div><p class="eyebrow">Maintenance run · ${escapeHtml(run.trigger)}</p><h2 class="run-title">${escapeHtml(run.scenarioId)}</h2><p class="phase">${escapeHtml(run.phase)}</p></div><span class="outcome ${outcomeClass(run.status)}">${escapeHtml(run.status)}</span></div><div class="run-summary"><p class="summary-copy">${escapeHtml(summary)}</p><div class="meta"><div><b>Advisory:</b> ${advisoryUrl ? `<a href="${advisoryUrl}" target="_blank" rel="noreferrer noopener">${escapeHtml(run.advisoryId)}</a>` : 'Not confirmed'}</div><div><b>Started:</b> ${escapeHtml(new Date(run.createdAt).toLocaleString())}</div><div><b>Sandbox snapshot:</b> ${escapeHtml(text(run.sandboxSnapshotId, 'Created only after sandbox staging'))}</div></div></div><div class="timeline-wrap"><div class="section-head"><h2>Evidence timeline</h2><p>Policy decides the repair; tools supply the proof.</p></div><div class="timeline">${renderTimeline(run)}</div></div><div class="evidence-section"><div class="section-head"><h2>Verified evidence</h2><p>${run.error ? 'Run stopped safely: ' + escapeHtml(run.error) : 'Only bounded, captured evidence is shown.'}</p></div><div class="evidence-grid"><article class="evidence-card"><div class="label">Dependency policy</div><h3>${run.advisoryId ? 'Exact repair authorized' : 'No repair authorization'}</h3><p class="muted">Only Microsoft.Extensions.Caching.Memory 8.0.0 → 8.0.1 is permitted in this workflow.</p>${diff?.output ? `<pre>${escapeHtml(diff.output)}</pre>` : ''}</article>${renderTavily(run)}<article class="evidence-card"><div class="label">NVIDIA analysis</div><h3>Risk explanation</h3><p>${escapeHtml(text(findStep(run, ['nvidia_risk_summary'])?.output, 'Waiting for policy-verified evidence.'))}</p></article><article class="evidence-card"><div class="label">Sandbox staging</div><h3>${sandbox?.status === 'ok' ? 'Isolated workspace prepared' : sandbox?.status === 'failed' ? 'Staging failed safely' : 'Awaiting sandbox'}</h3><p class="muted">${escapeHtml(sandbox?.detail || 'Nebius Sandbox is used only for the fixture and approved manifest patch.')}</p></article>${renderCommandCard('Baseline test', baseline, 'Tests begin only after sandbox staging.')}${renderCommandCard('Patched test', patched, 'Patch verification has not started.')}${renderCommandCard('Post-patch scan', scan, 'The clean scan is recorded after tests pass.')}</div></div>${renderManualCheckPanel(run)}<details class="trace"><summary>Technical tool trace (${(run.toolSteps || []).length} steps)</summary><div class="trace-list">${renderTrace(run)}</div></details>`;
    bindManualCheckButtons();
}
async function loadRun(id) {
    try {
        const run = await api(`/maintenance/runs/${encodeURIComponent(id)}`);
        if (run.status === 'Remediated' && run.sandboxSnapshotId) await ensureManualCheckDefinitions();
        renderRun(run);
        const manualCheck = activeManualCheck(run);
        runStatus.textContent = manualCheck ?
            `${run.status}: manual ${manualCheck.title.toLowerCase()} ${manualCheck.status.toLowerCase()}` :
            `${run.status}: ${run.phase}`;
        if (!terminal(run.status) || manualCheck) {
            runButton.disabled = true;
            newRun.classList.add('hidden');
            const lastStep = (run.toolSteps || []).at(-1);
            showActivity(manualCheck ? 'Manual Sandbox check in progress' : 'Maintenance run in progress',
                manualCheck ? `${manualCheck.title}: ${manualCheck.status}` : lastStep?.detail || run.phase);
            pollingId = window.setTimeout(() => loadRun(id), 1200);
        } else {
            pollingId = null;
            hideActivity();
            runButton.disabled = false;
            newRun.classList.remove('hidden');
            const latestManualCheck = (run.manualChecks || []).at(-1);
            const notificationKey =
                `${run.id}:${run.status}:${latestManualCheck?.id || ''}:${latestManualCheck?.status || ''}`;
            if (lastRunNotification !== notificationKey) {
                lastRunNotification = notificationKey;
                if (latestManualCheck && ['Passed', 'Failed'].includes(latestManualCheck.status)) {
                    const passed = latestManualCheck.status === 'Passed';
                    showToast(passed ? 'success' : 'error', passed ? 'Manual Sandbox check passed' :
                        'Manual Sandbox check failed', passed ?
                        `${latestManualCheck.title} returned the expected private response.` : latestManualCheck
                            .error || latestManualCheck.title);
                } else {
                    const detail = run.status === 'Remediated' ?
                        'Sandbox tests passed and the vulnerability scan is clean.' : run.error || run.phase;
                    showToast(run.status === 'Remediated' ? 'success' : 'error', run.status === 'Remediated' ?
                        'Repair verified' : `Run ${run.status.toLowerCase()} safely`, detail);
                }
            }
        }
    } catch (error) {
        pollingId = null;
        hideActivity();
        runButton.disabled = false;
        runStatus.textContent = `Action needs attention: ${error.message}`;
        showToast('error', 'Run update failed', error.message);
    }
}
async function startRun() {
    if (!preflightReady || pollingId) return;
    runButton.disabled = true;
    newRun.classList.add('hidden');
    showActivity('Starting maintenance run', 'Applying the fixed safety policy before any Sandbox action.');
    runStatus.textContent = 'Starting the selected policy-constrained path…';
    runPlaceholder.classList.remove('hidden');
    runPlaceholder.innerHTML =
        '<h3>Starting controlled run</h3><p>Applying the fixed safety policy before any Sandbox action.</p>';
    runPanel.classList.add('hidden');
    furthestStep = 3;
    goToStep(3);
    try {
        const result = await api(`/maintenance/runs?scenario=${encodeURIComponent(selectedScenario)}`, 'POST');
        await loadRun(result.id);
    } catch (error) {
        hideActivity();
        runButton.disabled = false;
        runStatus.textContent = `Action needs attention: ${error.message}`;
        runPlaceholder.innerHTML = `<h3>Run did not start</h3><p>${escapeHtml(error.message)}</p>`;
        showToast('error', 'Run did not start', error.message);
    }
}
bySelector('#preflightForm').addEventListener('submit', event => {
    event.preventDefault();
    refreshPreflight();
});
continueButton.addEventListener('click', () => {
    if (preflightReady) goToStep(2);
});
bySelector('#backToPreflight').addEventListener('click', () => goToStep(1));
runButton.addEventListener('click', startRun);
runButtons.forEach(button => button.addEventListener('click', () => {
    selectedScenario = button.dataset.scenario;
    startRun();
}));
newRun.addEventListener('click', () => goToStep(2));
bySelector('#startPath').addEventListener('click', () => goToStep(preflightReady ? 2 : 1));
bySelector('#operatorSignOut').addEventListener('click', async () => {
    await fetch('/operator/sign-out', {
        method: 'POST'
    });
    window.location.replace('/operator/sign-in');
});
[1, 2, 3].forEach(index => bySelector(`#indicator${index}`).addEventListener('click', () => goToStep(index)));
window.setTimeout(() => refreshPreflight(), 0);
