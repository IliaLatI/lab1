const result = document.querySelector("#result");


const profileResult = document.querySelector("#profile-result");

async function loadProfile() {
  profileResult.textContent = "";
  try {
    const response = await fetch("/api/me", { credentials: "same-origin" });
    if (response.status === 401) {
      profileResult.textContent = "";
      document.querySelector("#session-state").textContent = "Стан: не ввійшли";
      result.textContent = "Me: 401. Потрібен вхід.";
      return;
    }
    if (!response.ok || !response.headers.get("Content-Type")?.includes("application/json")) {
      profileResult.textContent = `Помилка профілю: ${response.status}`;
      return;
    }
    const profile = await response.json();
    profileResult.textContent = `Профіль: ${profile.userName}, ${profile.displayName}, ID: ${profile.id}`;
  } catch {
    profileResult.textContent = "Не вдалося отримати профіль.";
  }
}



document.querySelector("#login").addEventListener("submit", async event => {
  event.preventDefault();
  const form = event.currentTarget;
  const response = await fetch("/api/auth/login", { method: "POST", headers: { "Content-Type": "application/json" },
    body: JSON.stringify(Object.fromEntries(new FormData(form))) });
  form.elements.password.value = "";
  result.textContent = `Login: ${response.status}`;
  if (response.status === 204) await loadProfile();
  else profileResult.textContent = "";
  await refreshSessionState();
});
document.querySelector("#me").addEventListener("click", loadProfile);



document.querySelector("#logout").addEventListener("click", async () => {
  try {
    const response = await fetch("/api/auth/logout", {
      method: "POST", credentials: "same-origin"
    });


    if (response.status !== 204) {
      result.textContent = `Logout: ${response.status}`;
      return;
    }

    // Очищаємо дані попереднього сеансу.
    profileResult.textContent = "";
    document.querySelector("#incident").reset();
    incidentResult.textContent = "";


    // Повторно перевіряємо доступ до профілю.
    const check = await fetch("/api/me", { credentials: "same-origin" });

    result.textContent = `Logout: 204; Me: ${check.status}`;
    result.textContent += check.status === 401
      ? ". Стан: не ввійшли."
      : ". Потрібно перевірити стан сеансу.";
    await refreshSessionState();
  } catch {
    result.textContent = "Помилка перевірки виходу.";
  }
});


document.querySelector("#register").addEventListener("submit", async event => {
  event.preventDefault();
  const form = event.currentTarget;
  try {
    const response = await fetch("/api/auth/register", { method: "POST", headers: { "Content-Type": "application/json" },
      body: JSON.stringify(Object.fromEntries(new FormData(form))) });
    result.textContent = `Register: ${response.status}`;
    if (response.status === 201) result.textContent += ". Тепер увійдіть у систему.";
  } catch {
    result.textContent = "Помилка запиту реєстрації.";
  } finally {
    form.elements.password.value = "";
  }
});


const incidentResult = document.querySelector("#incident-result");

document.querySelector("#incident").addEventListener("submit", async event => {
  event.preventDefault();
  const form = event.currentTarget;

  // Отримуємо дані з форми.
  const data = Object.fromEntries(new FormData(form));

  // Перетворюємо локальну дату на UTC.
  const occurred = new Date(data.occurredAtUtc);
  if (Number.isNaN(occurred.getTime())) {
    incidentResult.textContent = "Неправильна дата та час.";
    return;
  }
  data.occurredAtUtc = occurred.toISOString();

  try {
    // Надсилаємо запит із cookie поточного користувача.
    const response = await fetch("/api/incidents", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      credentials: "same-origin",
      body: JSON.stringify(data)
    });

    // Перевіряємо результат створення.
    if (response.status === 201) {
      const created = await response.json();
      incidentResult.textContent = `Incident: 201, ID: ${created.id}`;
    } else {
      incidentResult.textContent = `Incident: ${response.status}`;
    }
  } catch {
    incidentResult.textContent = "Помилка створення інциденту.";
  }
});


async function refreshSessionState() {
  const state = document.querySelector("#session-state");

  try {
    const response = await fetch("/api/auth/session-check", { credentials: "same-origin" });

    if (response.status === 204) {
      state.textContent = "Стан: сеанс активний";
    } else if (response.status === 401) {
      state.textContent = "Стан: не ввійшли";
      profileResult.textContent = "";
    } else {
      state.textContent = `Стан: невідомий (${response.status})`;
      profileResult.textContent = "";
    }
  } catch {
    state.textContent = "Стан: помилка з'єднання";
    profileResult.textContent = "";

  }
}

// Перевіряємо стан під час відкриття сторінки.
refreshSessionState();
