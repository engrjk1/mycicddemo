const $ = (id) => document.getElementById(id);

async function api(path, options = {}) {
  const res = await fetch(path, { headers: { 'Content-Type': 'application/json' }, ...options });
  const body = await res.json().catch(() => null);
  return { ok: res.ok, status: res.status, body };
}

function showMessage(text, kind) {
  const el = $('message');
  el.textContent = text;
  el.className = `message ${kind}`;
}

// "2026-10-05T09:00:00" -> "2026-10-05 09:00"
const formatTime = (value) => value.replace('T', ' ').slice(0, 16);

function cell(text) {
  const td = document.createElement('td');
  td.textContent = text;
  return td;
}

async function loadProviders() {
  const { body: providers } = await api('/api/providers');
  for (const select of [$('provider'), $('filter')]) {
    for (const name of providers) {
      const option = document.createElement('option');
      option.value = name;
      option.textContent = name;
      select.append(option);
    }
  }
}

async function loadAppointments() {
  const provider = $('filter').value;
  const query = provider ? `?provider=${encodeURIComponent(provider)}` : '';
  const { body: appointments } = await api(`/api/appointments${query}`);
  const rows = $('rows');
  rows.replaceChildren();

  for (const a of appointments) {
    const tr = document.createElement('tr');
    tr.setAttribute('data-testid', 'appointment-row');
    tr.dataset.id = a.id;
    tr.append(cell(a.patientName), cell(a.provider), cell(formatTime(a.start)), cell(`${a.durationMinutes} min`), cell(a.reason || ''));

    const status = cell(a.status);
    status.setAttribute('data-testid', 'status');
    status.className = `status ${a.status.toLowerCase()}`;
    tr.append(status);

    const actions = document.createElement('td');
    if (a.status === 'Scheduled') {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'secondary';
      button.textContent = 'Cancel';
      button.setAttribute('data-testid', 'cancel-button');
      button.addEventListener('click', () => cancelAppointment(a.id));
      actions.append(button);
    }
    tr.append(actions);
    rows.append(tr);
  }
  $('empty').hidden = appointments.length > 0;
}

async function cancelAppointment(id) {
  const { ok, status, body } = await api(`/api/appointments/${id}/cancel`, { method: 'POST' });
  if (ok) showMessage(`Cancelled the appointment for ${body.patientName}.`, 'success');
  else showMessage(body?.errors?.join(' ') ?? `Could not cancel (HTTP ${status}).`, 'error');
  await loadAppointments();
}

$('booking-form').addEventListener('submit', async (event) => {
  event.preventDefault();
  let start = $('start').value;          // "2026-10-05T09:00"
  if (start.length === 16) start += ':00';

  const payload = {
    patientName: $('patientName').value,
    provider: $('provider').value,
    start: start || null,
    durationMinutes: Number($('duration').value),
    reason: $('reason').value || null,
  };

  const { ok, status, body } = await api('/api/appointments', { method: 'POST', body: JSON.stringify(payload) });
  if (ok) {
    showMessage(`Booked ${body.patientName} with ${body.provider} on ${formatTime(body.start)}.`, 'success');
    event.target.reset();
    await loadAppointments();
  } else {
    showMessage(body?.errors?.join(' ') ?? `Something went wrong (HTTP ${status}).`, 'error');
  }
});

$('filter').addEventListener('change', loadAppointments);

loadProviders().then(loadAppointments);
