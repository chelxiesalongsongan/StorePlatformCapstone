// Show/hide password fields.
document.addEventListener("click", (event) => {
  const button = event.target.closest("[data-toggle-password]");
  if (!button) return;
  const input = document.getElementById(button.dataset.togglePassword);
  if (!input) return;
  const show = input.type === "password";
  input.type = show ? "text" : "password";
  button.textContent = show ? "Hide" : "Show";
  button.setAttribute("aria-pressed", String(show));
});

// Show a loading label on submit buttons, but only when the form actually submits.
document.addEventListener("submit", (event) => {
  if (event.defaultPrevented) return; // client-side validation stopped it
  const button = event.target.querySelector("[data-loading-text]");
  if (!button) return;
  button.dataset.label = button.textContent;
  button.textContent = button.dataset.loadingText;
  button.disabled = true;
});

// Restore buttons when the user navigates back to the page.
window.addEventListener("pageshow", () => {
  document.querySelectorAll("[data-loading-text]").forEach((button) => {
    button.disabled = false;
    if (button.dataset.label) button.textContent = button.dataset.label;
  });
});
