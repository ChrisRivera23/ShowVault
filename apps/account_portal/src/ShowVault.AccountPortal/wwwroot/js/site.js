document.addEventListener("click", async (event) => {
  const button = event.target.closest("[data-copy-target]");
  if (!button) return;
  const output = document.getElementById(button.dataset.copyTarget);
  if (!output) return;
  await navigator.clipboard.writeText(output.textContent);
  button.textContent = "Copied";
});
