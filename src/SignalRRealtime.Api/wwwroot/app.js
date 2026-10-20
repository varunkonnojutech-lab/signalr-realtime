const statusEl = document.getElementById('connectionStatus');
const quoteGrid = document.getElementById('quoteGrid');
const messagesEl = document.getElementById('messages');
const userNameEl = document.getElementById('userName');
const roomNameEl = document.getElementById('roomName');
const joinButton = document.getElementById('joinButton');
const leaveButton = document.getElementById('leaveButton');
const messageForm = document.getElementById('messageForm');
const messageInput = document.getElementById('messageInput');
const presenceEl = document.getElementById('presenceCount');

const quoteCards = new Map();
let currentRoom = null;
let connection;

function setStatus(text, className) {
  statusEl.textContent = text;
  statusEl.className = `status ${className}`;
}

function addSystemMessage(text) {
  const item = document.createElement('li');
  item.className = 'system';
  item.textContent = text;
  messagesEl.appendChild(item);
  messagesEl.scrollTop = messagesEl.scrollHeight;
}

function addChatMessage(message) {
  const item = document.createElement('li');
  const meta = document.createElement('span');
  meta.className = 'meta';
  const sent = new Date(message.sentUtc).toLocaleTimeString();
  meta.textContent = `${message.userName} · #${message.room} · ${sent}`;

  const body = document.createElement('span');
  body.textContent = message.text;

  item.append(meta, body);
  messagesEl.appendChild(item);
  messagesEl.scrollTop = messagesEl.scrollHeight;
}

function renderQuote(quote) {
  let card = quoteCards.get(quote.symbol);
  if (!card) {
    card = document.createElement('article');
    card.className = 'quote';
    card.innerHTML = `
      <span class="symbol"></span>
      <span class="company"></span>
      <div class="price"></div>
      <span class="change"></span>
      <time></time>`;
    quoteGrid.appendChild(card);
    quoteCards.set(quote.symbol, card);
  }

  card.querySelector('.symbol').textContent = quote.symbol;
  card.querySelector('.company').textContent = quote.company;
  card.querySelector('.price').textContent = `$${Number(quote.price).toFixed(2)}`;

  const change = card.querySelector('.change');
  const changeValue = Number(quote.changePercent);
  change.textContent = `${changeValue >= 0 ? '▲' : '▼'} ${Math.abs(changeValue).toFixed(3)}% this tick`;
  change.className = `change ${changeValue >= 0 ? 'up' : 'down'}`;
  card.querySelector('time').textContent = `Updated ${new Date(quote.updatedUtc).toLocaleTimeString()}`;

  card.classList.add('updated');
  window.setTimeout(() => card.classList.remove('updated'), 220);
}

async function loadQuoteSnapshot() {
  const response = await fetch('/api/quotes');
  if (!response.ok) return;
  const quotes = await response.json();
  quotes.forEach(renderQuote);
}

function buildConnection() {
  connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/realtime')
    .withAutomaticReconnect([0, 2000, 5000, 10000])
    .configureLogging(signalR.LogLevel.Information)
    .build();

  connection.on('ReceiveQuote', renderQuote);
  connection.on('ReceiveMessage', addChatMessage);
  connection.on('ReceiveNotification', message => addSystemMessage(message));
  connection.on('UserJoined', (room, userName, count) => {
    addSystemMessage(`${userName} joined #${room}.`);
    if (room === currentRoom) presenceEl.textContent = `${count} online in room`;
  });
  connection.on('UserLeft', (room, userName, count) => {
    addSystemMessage(`${userName} left #${room}.`);
    if (room === currentRoom) presenceEl.textContent = `${count} online in room`;
  });
  connection.onreconnecting(() => setStatus('Reconnecting…', 'connecting'));
  connection.onreconnected(async () => {
    setStatus('Connected', 'connected');
    await joinDashboard();
    if (currentRoom) await joinRoom(currentRoom, false);
  });
  connection.onclose(async () => {
    setStatus('Disconnected', 'disconnected');
    window.setTimeout(start, 5000);
  });
}

async function joinDashboard() {
  await connection.invoke('JoinRoom', 'dashboard', userNameEl.value || 'dashboard-viewer');
}

async function joinRoom(room, announce = true) {
  const userName = userNameEl.value.trim() || 'Guest';
  await connection.invoke('JoinRoom', room, userName);
  currentRoom = room.trim().toLowerCase().replaceAll(' ', '-');
  roomNameEl.value = currentRoom;
  if (announce) addSystemMessage(`You joined #${currentRoom}.`);
}

async function start() {
  buildConnection();
  setStatus('Connecting…', 'connecting');
  try {
    await connection.start();
    setStatus('Connected', 'connected');
    await loadQuoteSnapshot();
    await joinDashboard();
    await joinRoom(roomNameEl.value || 'lobby');
  } catch (error) {
    console.error(error);
    setStatus('Disconnected', 'disconnected');
    window.setTimeout(start, 5000);
  }
}

joinButton.addEventListener('click', async () => {
  if (!connection || connection.state !== signalR.HubConnectionState.Connected) return;
  const nextRoom = roomNameEl.value.trim();
  if (!nextRoom) return;

  if (currentRoom && currentRoom !== nextRoom) {
    await connection.invoke('LeaveRoom', currentRoom, userNameEl.value);
  }
  await joinRoom(nextRoom);
});

leaveButton.addEventListener('click', async () => {
  if (!currentRoom || !connection) return;
  await connection.invoke('LeaveRoom', currentRoom, userNameEl.value);
  addSystemMessage(`You left #${currentRoom}.`);
  currentRoom = null;
  presenceEl.textContent = '0 online in room';
});

messageForm.addEventListener('submit', async event => {
  event.preventDefault();
  const text = messageInput.value.trim();
  if (!text || !currentRoom || !connection) return;

  await connection.invoke('SendMessage', currentRoom, userNameEl.value, text);
  messageInput.value = '';
  messageInput.focus();
});

start();
